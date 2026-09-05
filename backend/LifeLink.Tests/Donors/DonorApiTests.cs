using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LifeLink.Api.Controllers;
using LifeLink.Application.Auth;
using LifeLink.Application.Donors;
using LifeLink.Domain.Enums;
using LifeLink.Tests.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LifeLink.Tests.Donors;

public sealed class DonorApiTests : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client;
    public DonorApiTests(AuthApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task DonorCanCreateReadAndEvaluateOwnProfile()
    {
        var token = await RegisterDonorAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await _client.PostAsJsonAsync("/api/donors", new DonorProfileRequest(BloodType.OPositive, new DateOnly(1994, 4, 12), "Colombo", 6.9271m, 79.8612m, []));
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var donor = await create.Content.ReadFromJsonAsync<DonorView>(Json);
        Assert.NotNull(donor);
        var evaluation = await _client.PostAsync($"/api/donors/{donor!.Id}/check-eligibility", null);
        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);
        var result = await evaluation.Content.ReadFromJsonAsync<EligibilityEvaluation>(Json);
        Assert.Equal(EligibilityStatus.PendingVerification, result!.Status); // a donor self-check cannot grant Eligible; staff verify
        var history = await _client.GetFromJsonAsync<List<EligibilityHistoryView>>($"/api/donors/{donor.Id}/eligibility-history", Json);
        Assert.Single(history!);
    }

    [Fact]
    public async Task DonorCannotReadAnotherDonorsProfile()
    {
        var firstToken = await RegisterDonorAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var create = await _client.PostAsJsonAsync("/api/donors", new DonorProfileRequest(BloodType.APositive, new DateOnly(1993, 1, 1), "Kandy", null, null, []));
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var first = await create.Content.ReadFromJsonAsync<DonorView>(Json);
        var secondToken = await RegisterDonorAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        var forbidden = await _client.GetAsync($"/api/donors/{first!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ProfileWithoutDateOfBirthOrBloodTypeIsRejectedInsteadOfDefaulted()
    {
        var token = await RegisterDonorAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var noDob = await _client.PostAsJsonAsync("/api/donors", new { bloodType = "OPositive", address = "Colombo", medicalFlags = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, noDob.StatusCode);
        Assert.Contains("Date of birth is required", await noDob.Content.ReadAsStringAsync());
        var noBloodType = await _client.PostAsJsonAsync("/api/donors", new { dateOfBirth = "1990-01-01", address = "Colombo" });
        Assert.Equal(HttpStatusCode.BadRequest, noBloodType.StatusCode);
        Assert.Contains("Blood type is required", await noBloodType.Content.ReadAsStringAsync());
        var underage = await _client.PostAsJsonAsync("/api/donors", new { bloodType = "OPositive", dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd"), address = "Colombo" });
        Assert.Equal(HttpStatusCode.BadRequest, underage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/donors/me")).StatusCode);
    }

    private async Task<string> RegisterDonorAsync()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"donor-{Guid.NewGuid():N}@example.com", "StrongPassword!42", "Donor", "test"));
        var pair = await response.Content.ReadFromJsonAsync<TokenPair>();
        return pair!.AccessToken;
    }
}
