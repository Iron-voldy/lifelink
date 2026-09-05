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
