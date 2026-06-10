using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Catalog;

/// <summary>
/// Integration tests verifying that all Catalog POST endpoints return HTTP 201,
/// a Location header, and a response body containing the created resource ID.
/// These tests guard against CreatedAtAction routing regressions (SuppressAsyncSuffixInActionNames).
/// </summary>
[Trait("Category", "Integration")]
public sealed class CatalogPostEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task AuthorizeAsAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);
    }

    // ── POST /api/v1/markets ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateMarket_AsAdmin_Returns201WithLocationAndBody()
    {
        await AuthorizeAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = $"Test Market {Guid.NewGuid():N}",
            location = "Test Location",
            address = "123 Test Street",
            latitude = (decimal?)10.123,
            longitude = (decimal?)106.456
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("a Location header must be returned for 201 Created");

        var env = await response.Content.ReadFromJsonAsync<Envelope<CatalogItemBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateMarket_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = "Unauthorized Market"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateMarket_MissingName_Returns400()
    {
        await AuthorizeAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/v1/categories ──────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_AsAdmin_Returns201WithLocationAndBody()
    {
        await AuthorizeAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"Category {Guid.NewGuid():N}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("a Location header must be returned for 201 Created");

        var env = await response.Content.ReadFromJsonAsync<Envelope<CatalogItemBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCategory_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Unauthorized Category"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCategory_MissingName_Returns400()
    {
        await AuthorizeAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/v1/units ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUnit_AsAdmin_Returns201WithLocationAndBody()
    {
        await AuthorizeAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/units", new
        {
            name = $"Unit {Guid.NewGuid():N}",
            abbreviation = "u"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("a Location header must be returned for 201 Created");

        var env = await response.Content.ReadFromJsonAsync<Envelope<CatalogItemBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateUnit_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/v1/units", new
        {
            name = "Unauthorized Unit"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUnit_MissingName_Returns400()
    {
        await AuthorizeAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/units", new
        {
            name = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/v1/products ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateProduct_AsAdmin_Returns201WithLocationAndBody()
    {
        await AuthorizeAsAdminAsync();

        // First create a unit to reference
        var unitResp = await _client.PostAsJsonAsync("/api/v1/units", new
        {
            name = $"ProdUnit {Guid.NewGuid():N}",
            abbreviation = "pu"
        });
        unitResp.EnsureSuccessStatusCode();
        var unitEnv = await unitResp.Content.ReadFromJsonAsync<Envelope<CatalogItemBody>>();

        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"Product {Guid.NewGuid():N}",
            unitId = unitEnv!.Data!.Id,
            categoryId = (Guid?)null,
            description = "Test product"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("a Location header must be returned for 201 Created");

        var env = await response.Content.ReadFromJsonAsync<Envelope<CatalogItemBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateProduct_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Unauthorized Product",
            unitId = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateProduct_MissingName_Returns400()
    {
        await AuthorizeAsAdminAsync();

        // First create a unit to reference
        var unitResp = await _client.PostAsJsonAsync("/api/v1/units", new
        {
            name = $"ProdUnit2 {Guid.NewGuid():N}",
            abbreviation = "pu2"
        });
        unitResp.EnsureSuccessStatusCode();
        var unitEnv = await unitResp.Content.ReadFromJsonAsync<Envelope<CatalogItemBody>>();

        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = (string?)null,
            unitId = unitEnv!.Data!.Id
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

// ── Private DTOs ─────────────────────────────────────────────────────────────

/// <summary>Minimal DTO to extract the ID from any Catalog create response data.</summary>
file sealed record CatalogItemBody(Guid Id);
