using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Orders;

/// <summary>
/// SCRUM-364 — GET /api/v1/restaurants/{restaurantId}/credit/statements/{statementId}/pdf.
/// Same RBAC + IDOR guard as GetStatementAsync (GetCreditStatementQueryHandler), just rendered
/// to PDF instead of JSON.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CreditStatementPdfEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();

    private readonly HttpClient _client = factory.CreateClient();

    private static string Endpoint(Guid restaurantId, Guid statementId) =>
        $"/api/v1/restaurants/{restaurantId}/credit/statements/{statementId}/pdf";

    [Fact]
    public async Task GetStatementPdf_AsOwner_Returns200PdfAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        var restaurant = await CreateRestaurantAsync(adminToken);
        var restaurantToken = await LoginAsync(restaurant.Email, restaurant.Password);
        var statementId = await GenerateStatementAsync(restaurant.RestaurantId, restaurantToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", restaurantToken);

        // Act
        var response = await _client.GetAsync(Endpoint(restaurant.RestaurantId, statementId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(PdfMagic.Length).Should().Equal(PdfMagic);
    }

    [Fact]
    public async Task GetStatementPdf_AsAdmin_Returns200PdfAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        var restaurant = await CreateRestaurantAsync(adminToken);
        var restaurantToken = await LoginAsync(restaurant.Email, restaurant.Password);
        var statementId = await GenerateStatementAsync(restaurant.RestaurantId, restaurantToken);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.GetAsync(Endpoint(restaurant.RestaurantId, statementId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(PdfMagic.Length).Should().Equal(PdfMagic);
    }

    [Fact]
    public async Task GetStatementPdf_DifferentRestaurantOwner_Returns404Async()
    {
        // Arrange — restaurant A owns the statement; restaurant B requests it using B's own
        // restaurantId (passes the ownership check) but A's statementId. The IDOR guard on
        // the by-id lookup (statement.RestaurantId != request.RestaurantId) must 404, not
        // leak A's statement to B.
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        var restaurantA = await CreateRestaurantAsync(adminToken);
        var tokenA = await LoginAsync(restaurantA.Email, restaurantA.Password);
        var statementIdA = await GenerateStatementAsync(restaurantA.RestaurantId, tokenA);

        var restaurantB = await CreateRestaurantAsync(adminToken);
        var tokenB = await LoginAsync(restaurantB.Email, restaurantB.Password);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Act
        var response = await _client.GetAsync(Endpoint(restaurantB.RestaurantId, statementIdA));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("CREDITSTATEMENT_NOT_FOUND");
    }

    [Fact]
    public async Task GetStatementPdf_UnknownStatementId_Returns404Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        var restaurant = await CreateRestaurantAsync(adminToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.GetAsync(Endpoint(restaurant.RestaurantId, Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("CREDITSTATEMENT_NOT_FOUND");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return env!.Data!.AccessToken;
    }

    private async Task<CreatedRestaurant> CreateRestaurantAsync(string adminToken)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"pdf-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = $"PDF Test Restaurant {Guid.NewGuid():N}"
        });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        var restaurantId = body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;

        return new CreatedRestaurant(restaurantId, email, password);
    }

    private async Task<Guid> GenerateStatementAsync(Guid restaurantId, string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.PostAsJsonAsync(
            $"/api/v1/restaurants/{restaurantId}/credit/statements/generate",
            new { year = 2024, month = 1 });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<StatementBody>>();
        return env!.Data!.Id;
    }

    private sealed record CreatedRestaurant(Guid RestaurantId, string Email, string Password);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
    private sealed record StatementBody(Guid Id);
}
