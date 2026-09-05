using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LifeLink.Api.Infrastructure;
using LifeLink.Infrastructure.Authentication;
using LifeLink.Tests.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LifeLink.Tests.Api;

public sealed class ErrorHandlingTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task InvalidBodyReturnsFieldLevelMessagesAndCorrelationId()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", body.GetProperty("title").GetString());
        var errors = body.GetProperty("errors");
        Assert.Contains("valid email", errors.GetProperty("email")[0].GetString());
        Assert.Equal("Password is required.", errors.GetProperty("password")[0].GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task MalformedJsonReturnsReadableValidationErrorInsteadOfServerError()
    {
        var response = await _client.PostAsync("/api/auth/login", new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", body.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("Donor,BloodBankAdmin")]
    public async Task RegisterAcceptsOnlyRoleNames(string role)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email = $"role-{Guid.NewGuid():N}@example.com", password = "Strong!Pass123", role });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownEnumValueReportsOnlyTheOffendingField()
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register", new { email = $"enum-{Guid.NewGuid():N}@example.com", password = "Strong!Pass123", role = "Donor" });
        var token = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/donors") { Content = JsonContent.Create(new { bloodType = "Purple", dateOfBirth = "1990-01-01", address = "Colombo", medicalFlags = Array.Empty<string>() }) };
        request.Headers.Authorization = new("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("bloodType", out _));
        Assert.False(errors.TryGetProperty("request", out _), errors.ToString());
    }

    [Fact]
    public async Task OversizedOrUnsafeCorrelationIdIsReplaced()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("X-Correlation-ID", new string('a', 500));
        var response = await _client.SendAsync(request);
        Assert.True(response.Headers.GetValues("X-Correlation-ID").Single().Length <= 100);
    }

    [Theory]
    [InlineData("Production", "development-only-signing-key-change-in-production-2026", "agent-key-that-is-long-enough", true)]
    [InlineData("Production", "a-real-random-signing-key-with-plenty-of-length-1234", "replace-with-a-random-internal-key", true)]
    [InlineData("Production", "short", "agent-key-that-is-long-enough", true)]
    [InlineData("Production", "a-real-random-signing-key-with-plenty-of-length-1234", "agent-key-that-is-long-enough", false)]
    [InlineData("Development", "development-only-signing-key-change-in-production-2026", "replace-with-a-random-internal-key", false)]
    public void StartupChecksRejectWeakOrPlaceholderSecretsOutsideDevelopment(string environment, string signingKey, string agentKey, bool shouldThrow)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AgentService:InternalApiKey"] = agentKey }).Build();
        var act = () => StartupSecurityChecks.Validate(configuration, new TestEnvironment(environment), new JwtOptions { SigningKey = signingKey });
        if (shouldThrow) Assert.Throws<InvalidOperationException>(act); else act();
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
