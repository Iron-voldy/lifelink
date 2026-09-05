using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeLink.Application.Auth;
using LifeLink.Api.Controllers;
using LifeLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeLink.Application.Requests;

namespace LifeLink.Tests.Authentication;

public sealed class AuthApiTests : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client;
    public AuthApiTests(AuthApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Me_WithoutTokenReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterThenMe_ReturnsAuthenticatedIdentity()
    {
        var email = $"donor-{Guid.NewGuid():N}@example.com";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "StrongPassword!42", "Donor", "integration-test"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var tokens = await register.Content.ReadFromJsonAsync<TokenPair>();
        Assert.NotNull(tokens);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<AuthenticatedUser>(Json);
        Assert.Equal(email, me!.Email);
    }

    [Fact]
    public async Task Register_AdminReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("admin@example.com", "StrongPassword!42", "BloodBankAdmin", null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"auth-api-{Guid.NewGuid():N}";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LifeLinkDbContext>>();
            services.RemoveAll<LifeLinkDbContext>();
            services.AddDbContext<LifeLinkDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IRequestWorkflowTrigger>();
            services.AddSingleton<IRequestWorkflowTrigger, TestWorkflowTrigger>();
        });
    }
    private sealed class TestWorkflowTrigger : IRequestWorkflowTrigger { public Task TriggerAsync(Guid requestId, string reason, CancellationToken ct) => Task.CompletedTask; }
}
