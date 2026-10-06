using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeLink.Api.Controllers;
using LifeLink.Application.Auth;
using LifeLink.Application.Inventory;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using LifeLink.Tests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LifeLink.Tests.Inventory;

public sealed class InventoryApiTests : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly AuthApiFactory _factory; private readonly HttpClient _client;
    public InventoryApiTests(AuthApiFactory factory) { _factory = factory; _client = factory.CreateClient(); }

    [Fact]
    public async Task AdminCanCreateLocationStockInAndReadReport()
    {
        var token = await AdminTokenAsync(); _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var locationResponse = await _client.PostAsJsonAsync("/api/inventory/locations", new LocationRequest("National Blood Centre", "Colombo", 6.9m, 79.8m)); Assert.Equal(HttpStatusCode.Created, locationResponse.StatusCode);
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationView>(Json);
        var stock = await _client.PostAsJsonAsync("/api/inventory/stock-in", new StockInRequest(location!.Id, BloodType.BPositive, 10, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), "Synthetic seed")); Assert.Equal(HttpStatusCode.Created, stock.StatusCode);
        var report = await _client.GetFromJsonAsync<List<StockLevelView>>("/api/inventory/reports/stock-levels", Json); Assert.Contains(report!, x => x.LocationId == location.Id && x.AvailableUnits == 10);
    }

    [Fact]
    public async Task DonorCannotMutateInventory()
    {
        var registration = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"donor-{Guid.NewGuid():N}@example.com", "StrongPassword!42", "Donor", null)); var token = (await registration.Content.ReadFromJsonAsync<TokenPair>())!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/inventory/locations", new LocationRequest("Denied", "Nowhere", 0, 0)); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LocationValidationRejectsInvalidCoordinatesLengthsAndMissingAddress()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());

        var invalidCoordinates = await _client.PostAsJsonAsync("/api/inventory/locations", new
        {
            name = "Invalid coordinates",
            address = "Colombo",
            latitude = 91m,
            longitude = 79.8m
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCoordinates.StatusCode);
        Assert.Contains("latitude", await ValidationErrorsAsync(invalidCoordinates));

        var missingAddress = await _client.PostAsJsonAsync("/api/inventory/locations", new
        {
            name = "Missing address",
            latitude = 6.9m,
            longitude = 79.8m
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingAddress.StatusCode);
        Assert.Contains("address", await ValidationErrorsAsync(missingAddress));

        var longName = await _client.PostAsJsonAsync("/api/inventory/locations", new
        {
            name = new string('n', 251),
            address = "Colombo",
            latitude = 6.9m,
            longitude = 79.8m
        });
        Assert.Equal(HttpStatusCode.BadRequest, longName.StatusCode);
        Assert.Contains("name", await ValidationErrorsAsync(longName));

        var longAddress = await _client.PostAsJsonAsync("/api/inventory/locations", new
        {
            name = "Long address",
            address = new string('a', 501),
            latitude = 6.9m,
            longitude = 79.8m
        });
        Assert.Equal(HttpStatusCode.BadRequest, longAddress.StatusCode);
        Assert.Contains("address", await ValidationErrorsAsync(longAddress));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public async Task StockInValidationRejectsUnitsOutsideAllowedRange(int units)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());
        var locationId = await CreateLocationAsync();

        var response = await _client.PostAsJsonAsync("/api/inventory/stock-in", new
        {
            locationId,
            bloodType = BloodType.APositive,
            units,
            expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            source = "API validation test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("units", await ValidationErrorsAsync(response));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(367)]
    public async Task StockInValidationRejectsExpiredOrOverlongShelfLife(int expiryOffsetDays)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());
        var locationId = await CreateLocationAsync();
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(expiryOffsetDays);

        var response = await _client.PostAsJsonAsync("/api/inventory/stock-in", new StockInRequest(
            locationId, BloodType.APositive, 1, expiry, "API validation test"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_stock", problem.GetProperty("title").GetString());
        Assert.Contains("Expiry date", problem.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData(0, "valid-key", 30)]
    [InlineData(101, "valid-key", 30)]
    [InlineData(1, "valid-key", 4)]
    [InlineData(1, "valid-key", 1441)]
    [InlineData(1, "", 30)]
    [InlineData(1, "this-idempotency-key-is-longer-than-sixty-four-characters-xxxxxxxx", 30)]
    public async Task ReservationValidationRejectsInvalidBounds(int units, string idempotencyKey, int holdMinutes)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/inventory/reserve", new
        {
            bloodRequestId = Guid.NewGuid(),
            units,
            idempotencyKey,
            holdMinutes
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task InventoryEndpointsRequireAuthentication()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/inventory/locations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync(
            "/api/inventory/locations", new LocationRequest("Unauthorized", "Colombo", 6.9m, 79.8m))).StatusCode);
    }

    [Theory]
    [InlineData("Donor")]
    [InlineData("HospitalRequester")]
    public async Task AuthenticatedNonAdminCanReadButCannotMutateInventory(string role)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RegisteredUserTokenAsync(role));

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/inventory/locations")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/inventory/compatibility/APositive")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync(
            "/api/inventory/locations", new LocationRequest("Denied", "Colombo", 6.9m, 79.8m))).StatusCode);
    }

    [Fact]
    public async Task AdminCanReadInventoryAndCreateLocation()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/inventory/locations")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(
            "/api/inventory/locations", new LocationRequest("Admin location", "Colombo", 6.9m, 79.8m))).StatusCode);
    }

    private async Task<Guid> CreateLocationAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/inventory/locations",
            new LocationRequest($"Location-{Guid.NewGuid():N}", "Colombo", 6.9m, 79.8m));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LocationView>(Json))!.Id;
    }

    private async Task<string> RegisteredUserTokenAsync(string role)
    {
        var registration = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com", "StrongPassword!42", role, null));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        return (await registration.Content.ReadFromJsonAsync<TokenPair>())!.AccessToken;
    }

    private static async Task<string[]> ValidationErrorsAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", problem.GetProperty("title").GetString());
        return problem.GetProperty("errors").EnumerateObject().Select(x => x.Name).ToArray();
    }

    private async Task<string> AdminTokenAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LifeLinkDbContext>(); var user = new User(email, "placeholder", UserRole.BloodBankAdmin); user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, "StrongPassword!42")); db.Users.Add(user); await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "StrongPassword!42", "test")); return (await login.Content.ReadFromJsonAsync<TokenPair>())!.AccessToken;
    }
}
