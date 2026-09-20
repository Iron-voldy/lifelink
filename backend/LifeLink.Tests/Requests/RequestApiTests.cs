using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeLink.Api.Controllers;
using LifeLink.Application.Auth;
using LifeLink.Application.Requests;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using LifeLink.Tests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LifeLink.Tests.Requests;

public sealed class RequestApiTests : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client;
    private readonly AuthApiFactory _factory;
    public RequestApiTests(AuthApiFactory factory) { _factory = factory; _client = factory.CreateClient(); }

    [Fact]
    public async Task VerifiedHospitalCanCreateEscalateAndAuditRequest()
    {
        var token = await RegisterHospitalUserAsync(); _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var hospitalResponse = await _client.PostAsJsonAsync("/api/hospitals/register", new HospitalRegistrationRequest("Test Hospital", $"REG-{Guid.NewGuid():N}", "Colombo", null, null, "Medical Officer"));
        Assert.Equal(HttpStatusCode.Created, hospitalResponse.StatusCode);
        var hospital = await hospitalResponse.Content.ReadFromJsonAsync<HospitalView>(Json); await VerifyInDatabaseAsync(hospital!.Id);
        var create = await _client.PostAsJsonAsync("/api/requests", new BloodRequestInput(BloodType.ONegative, 6, RequestUrgency.Routine, "Urgent theatre requirement", DateTimeOffset.UtcNow.AddHours(8)));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var request = await create.Content.ReadFromJsonAsync<BloodRequestView>(Json); Assert.Equal(RequestUrgency.Critical, request!.Urgency);
        var escalation = await _client.PostAsJsonAsync($"/api/requests/{request.Id}/escalate", new EscalationRequest("Patient condition worsened"));
        Assert.Equal(HttpStatusCode.OK, escalation.StatusCode);
        var history = await _client.GetFromJsonAsync<List<RequestHistoryView>>($"/api/requests/{request.Id}/history", Json);
        Assert.Equal(2, history!.Count);
    }

    [Fact]
    public async Task HospitalCannotReadAnotherHospitalsRequest()
    {
        var firstToken = await RegisterHospitalUserAsync(); _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var h1Response = await _client.PostAsJsonAsync("/api/hospitals/register", new HospitalRegistrationRequest("First Hospital", $"REG-{Guid.NewGuid():N}", "Galle", null, null, "Doctor"));
        var h1 = await h1Response.Content.ReadFromJsonAsync<HospitalView>(Json); await VerifyInDatabaseAsync(h1!.Id);
        var create = await _client.PostAsJsonAsync("/api/requests", new BloodRequestInput(BloodType.APositive, 1, RequestUrgency.Routine, null, DateTimeOffset.UtcNow.AddDays(1)));
        var request = await create.Content.ReadFromJsonAsync<BloodRequestView>(Json);
        var secondToken = await RegisterHospitalUserAsync(); _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        var h2 = await _client.PostAsJsonAsync("/api/hospitals/register", new HospitalRegistrationRequest("Second Hospital", $"REG-{Guid.NewGuid():N}", "Kandy", null, null, "Doctor")); Assert.Equal(HttpStatusCode.Created, h2.StatusCode);
        var forbidden = await _client.GetAsync($"/api/requests/{request!.Id}"); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task HospitalStaffCanReadOwnHospitalStatusAndOtherRolesAreForbiddenFromRequestLists()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterHospitalUserAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/hospitals/me")).StatusCode);
        var registered = await _client.PostAsJsonAsync("/api/hospitals/register", new HospitalRegistrationRequest("Status Hospital", $"REG-{Guid.NewGuid():N}", "Matara", null, null, "Doctor"));
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var mine = await _client.GetFromJsonAsync<HospitalView>("/api/hospitals/me", Json);
        Assert.Equal(VerificationStatus.Pending, mine!.VerificationStatus);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterHospitalUserAsync("Donor"));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/requests/reports/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/hospitals/me")).StatusCode);
    }

    [Fact]
    public async Task HospitalCannotWithdrawADispatchedRequest()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterHospitalUserAsync());
        var hospitalResponse = await _client.PostAsJsonAsync("/api/hospitals/register", new HospitalRegistrationRequest("Dispatch Hospital", $"REG-{Guid.NewGuid():N}", "Galle", null, null, "Doctor"));
        var hospital = await hospitalResponse.Content.ReadFromJsonAsync<HospitalView>(Json); await VerifyInDatabaseAsync(hospital!.Id);
        var create = await _client.PostAsJsonAsync("/api/requests", new BloodRequestInput(BloodType.BPositive, 1, RequestUrgency.Routine, null, DateTimeOffset.UtcNow.AddDays(1)));
        var request = await create.Content.ReadFromJsonAsync<BloodRequestView>(Json);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LifeLinkDbContext>();
            (await db.BloodRequests.SingleAsync(x => x.Id == request!.Id)).TransitionTo(BloodRequestStatus.Dispatched); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/requests/{request!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync($"/api/requests/{request.Id}/escalate", new EscalationRequest(null))).StatusCode);
    }

    private async Task<string> RegisterHospitalUserAsync(string role = "HospitalRequester")
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"staff-{Guid.NewGuid():N}@example.com", "StrongPassword!42", role, "test"));
        return (await response.Content.ReadFromJsonAsync<TokenPair>())!.AccessToken;
    }
    private async Task VerifyInDatabaseAsync(Guid hospitalId)
    {
        using var scope = _factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<LifeLinkDbContext>();
        var hospital = await db.Hospitals.SingleAsync(x => x.Id == hospitalId); hospital.SetVerification(VerificationStatus.Verified); await db.SaveChangesAsync();
    }
}
