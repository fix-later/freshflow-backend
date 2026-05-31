using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Integration")]
public sealed class ApproveRestaurantEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> LoginAsAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ApproveTokenPair>();
        return body!.AccessToken;
    }

    private async Task<Guid> CreateRestaurantUserAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = $"restaurant{Guid.NewGuid():N}@test.com",
            password = "RestaurantP@ss1",
            role = "restaurant",
            restaurantName = "Test Restaurant"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CreatedUserResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task ApproveRestaurant_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PatchAsync(
            $"/api/v1/admin/restaurants/{Guid.NewGuid()}/approve", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApproveRestaurant_NotFound_Returns404()
    {
        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PatchAsync(
            $"/api/v1/admin/restaurants/{Guid.NewGuid()}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file sealed record ApproveTokenPair(string AccessToken, string RefreshToken, int ExpiresIn);
file sealed record CreatedUserResponse(Guid Id, string Email, string Role, bool IsActive, DateTime CreatedAt);
