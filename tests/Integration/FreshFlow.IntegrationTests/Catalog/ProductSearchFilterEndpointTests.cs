using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Catalog;

/// <summary>
/// Integration tests for the product search (M1) and category-filter (M2) improvements
/// in ProductRepository.GetPagedAsync.
/// <list type="bullet">
///   <item>M1: <c>EF.Functions.ILike</c> makes name search case-insensitive.</item>
///   <item>M2: Category param can be a Guid (matches <c>CategoryId</c>) or a plain string
///     (matches <c>LegacyCategory</c>); both must return the same product.</item>
/// </list>
/// Each test seeds its own uniquely-named data (via Guid tag suffix) to remain
/// independent of other test classes that share the same <see cref="AuthWebAppFactory"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProductSearchFilterEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

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
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>(JsonOpts);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);
    }

    private async Task<Guid> CreateUnitAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/units", new { name, abbreviation = "u" });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<ProductSearchIdBody>>(JsonOpts);
        return env!.Data!.Id;
    }

    private async Task<Guid> CreateCategoryAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/categories", new { name });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<ProductSearchIdBody>>(JsonOpts);
        return env!.Data!.Id;
    }

    private async Task<Guid> CreateProductAsync(string name, Guid unitId, Guid? categoryId = null)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name,
            unitId,
            categoryId,
            description = (string?)null
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<ProductSearchIdBody>>(JsonOpts);
        return env!.Data!.Id;
    }

    private async Task<ProductSearchListBody> GetProductsAsync(string query)
    {
        var resp = await _client.GetAsync($"/api/v1/products?{query}&page=1&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<Envelope<ProductSearchListBody>>(JsonOpts);
        return env!.Data!;
    }

    // ── M1: case-insensitive search via ILike ─────────────────────────────────

    [Fact]
    public async Task GetProducts_SearchByLowercase_FindsProductCreatedWithUppercaseName()
    {
        // Arrange
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var unitId = await CreateUnitAsync($"SrchUnit-{tag}");
        var productId = await CreateProductAsync($"ILIKE-UPPER-{tag}", unitId);

        // Act — search using the lower-case variant
        var body = await GetProductsAsync(
            $"search={Uri.EscapeDataString($"ilike-upper-{tag}")}");

        // Assert
        body.Data.Should().Contain(p => p.Id == productId,
            because: "ILike should match names case-insensitively");
    }

    [Fact]
    public async Task GetProducts_SearchByUppercase_FindsProductCreatedWithLowercaseName()
    {
        // Arrange
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var unitId = await CreateUnitAsync($"SrchUnit2-{tag}");
        var productId = await CreateProductAsync($"ilike-lower-{tag}", unitId);

        // Act — search using the upper-case variant
        var body = await GetProductsAsync(
            $"search={Uri.EscapeDataString($"ILIKE-LOWER-{tag}")}");

        // Assert
        body.Data.Should().Contain(p => p.Id == productId,
            because: "ILike should match names case-insensitively");
    }

    [Fact]
    public async Task GetProducts_Search_DoesNotReturnUnrelatedProducts()
    {
        // Arrange
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var unitId = await CreateUnitAsync($"SrchUnit3-{tag}");
        await CreateProductAsync($"NOMATCH-{tag}", unitId);

        // Act — search for a term that cannot match any product
        var body = await GetProductsAsync(
            $"search={Uri.EscapeDataString($"ZZZDISTINCT-NOTFOUND-{tag}")}");

        // Assert
        body.Data.Should().BeEmpty(
            because: "no product name contains the search term");
    }

    // ── M2: category filter by Guid ───────────────────────────────────────────

    [Fact]
    public async Task GetProducts_FilterByCategoryGuid_ReturnsProductsWithMatchingCategoryId()
    {
        // Arrange
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var unitId = await CreateUnitAsync($"CatUnit-{tag}");
        var catId = await CreateCategoryAsync($"Seafood-{tag}");
        var productId = await CreateProductAsync($"Fish-{tag}", unitId, catId);

        // Act — filter using the category's Guid value
        var body = await GetProductsAsync($"category={catId}");

        // Assert
        body.Data.Should().Contain(p => p.Id == productId,
            because: "product's CategoryId matches the queried Guid");
    }

    [Fact]
    public async Task GetProducts_FilterByCategoryGuid_ExcludesProductsFromDifferentCategory()
    {
        // Arrange
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var unitId = await CreateUnitAsync($"CatUnit2-{tag}");
        var catId = await CreateCategoryAsync($"Dairy-{tag}");
        var otherCatId = await CreateCategoryAsync($"Meat-{tag}");
        var dairyProductId = await CreateProductAsync($"Milk-{tag}", unitId, catId);
        var meatProductId = await CreateProductAsync($"Beef-{tag}", unitId, otherCatId);

        // Act — filter by Dairy category only
        var body = await GetProductsAsync($"category={catId}");

        // Assert
        body.Data.Should().Contain(p => p.Id == dairyProductId,
            because: "Milk belongs to the Dairy category");
        body.Data.Should().NotContain(p => p.Id == meatProductId,
            because: "Beef belongs to a different category");
    }

    // ── M2: backward compat — category filter by legacy string name ───────────

    [Fact]
    public async Task GetProducts_FilterByCategoryName_ReturnsProductsWhoseLegacyCategoryMatches()
    {
        // Arrange
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var unitId = await CreateUnitAsync($"CatUnit3-{tag}");
        var catName = $"Fruit-{tag}";
        var catId = await CreateCategoryAsync(catName);
        // CreateProductCommandHandler auto-sets LegacyCategory = category.Name.
        var productId = await CreateProductAsync($"Apple-{tag}", unitId, catId);

        // Act — filter by the category NAME (plain string, not a Guid)
        var body = await GetProductsAsync($"category={Uri.EscapeDataString(catName)}");

        // Assert
        body.Data.Should().Contain(p => p.Id == productId,
            because: "product's LegacyCategory was set to the category name and should match");
    }

    // ── Nested private DTOs ───────────────────────────────────────────────────

    private sealed record ProductSearchIdBody(Guid Id);

    private sealed record ProductSearchListBody(
        IReadOnlyList<ProductSearchItem> Data,
        ProductSearchMeta Meta);

    private sealed record ProductSearchItem(Guid Id, string Name, Guid? CategoryId);

    private sealed record ProductSearchMeta(int Total, int Page, int PageSize);
}
