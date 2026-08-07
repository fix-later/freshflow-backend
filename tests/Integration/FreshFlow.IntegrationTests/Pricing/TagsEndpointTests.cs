using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Pricing;

/// <summary>
/// SCRUM-386 — /api/v1/tags CRUD, and the cascade/RESTRICT behavior of the
/// market_product_tags join table.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TagsEndpointTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    private const string Endpoint = "/api/v1/tags";
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> LoginAsAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "admin@test.freshflow", password = "AdminP@ss1" });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return env!.Data!.AccessToken;
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTag_Admin_Returns201AndNormalizesNameAsync()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());
        var name = $"  Khuyến Mãi {Guid.NewGuid().ToString("N")[..6]} ";

        // Act
        var response = await _client.PostAsJsonAsync(Endpoint, new { name, pinsToTop = false });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var env = await response.Content.ReadFromJsonAsync<Envelope<TagBody>>();
        env!.Data!.Name.Should().Be(name.Trim().ToLowerInvariant());
        env.Data.PinsToTop.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTag_DuplicateLiveName_Returns409Async()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());
        var name = $"dup-{Guid.NewGuid().ToString("N")[..8]}";
        await _client.PostAsJsonAsync(Endpoint, new { name, pinsToTop = false });

        // Act — same name again
        var response = await _client.PostAsJsonAsync(Endpoint, new { name, pinsToTop = false });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("TAG_NAME_CONFLICT");
    }

    [Fact]
    public async Task GetTags_AnyAuthenticatedUser_Returns200Async()
    {
        // Arrange — restaurant role, not admin
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RegisterAndLoginRestaurantAsync());

        // Act
        var response = await _client.GetAsync(Endpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTag_NonAdmin_Returns403Async()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RegisterAndLoginRestaurantAsync());

        // Act
        var response = await _client.PostAsJsonAsync(
            Endpoint, new { name = $"forbidden-{Guid.NewGuid().ToString("N")[..8]}", pinsToTop = false });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTag_Admin_RenamesAndTogglesPinAsync()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());
        var createResp = await _client.PostAsJsonAsync(
            Endpoint, new { name = $"before-{Guid.NewGuid().ToString("N")[..8]}", pinsToTop = false });
        var created = (await createResp.Content.ReadFromJsonAsync<Envelope<TagBody>>())!.Data!;
        var newName = $"after-{Guid.NewGuid().ToString("N")[..8]}";

        // Act
        var response = await _client.PutAsJsonAsync(
            $"{Endpoint}/{created.Id}", new { name = newName, pinsToTop = true });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<Envelope<TagBody>>();
        env!.Data!.Name.Should().Be(newName.ToLowerInvariant());
        env.Data.PinsToTop.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTag_Admin_SoftDeletesAndRemovesFromListAsync()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());
        var createResp = await _client.PostAsJsonAsync(
            Endpoint, new { name = $"todelete-{Guid.NewGuid().ToString("N")[..8]}", pinsToTop = false });
        var created = (await createResp.Content.ReadFromJsonAsync<Envelope<TagBody>>())!.Data!;

        // Act
        var deleteResponse = await _client.DeleteAsync($"{Endpoint}/{created.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var listResp = await _client.GetAsync(Endpoint);
        var listEnv = await listResp.Content.ReadFromJsonAsync<Envelope<List<TagBody>>>();
        listEnv!.Data!.Should().NotContain(t => t.Id == created.Id);
    }

    // ── Delete-while-in-use clears assignments (RESTRICT on tag_id is satisfied
    //    because the handler clears join rows before soft-deleting the tag) ────

    [Fact]
    public async Task DeleteTag_InUseByAListing_ClearsAssignmentAndSucceedsAsync()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());
        var createResp = await _client.PostAsJsonAsync(
            Endpoint, new { name = $"inuse-{Guid.NewGuid().ToString("N")[..8]}", pinsToTop = false });
        var tag = (await createResp.Content.ReadFromJsonAsync<Envelope<TagBody>>())!.Data!;

        var marketId = await CreateMarketAsync($"IT-Mkt-TagDel-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"TagDel-{Guid.NewGuid():N}", unitId);
        var marketProductId = await SeedMarketProductAsync(marketId, productId, 10_000m, 10, [tag.Id]);

        // Act
        var deleteResponse = await _client.DeleteAsync($"{Endpoint}/{tag.Id}");

        // Assert — delete succeeds (RESTRICT never fires: join row was cleared first)
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mp = await db.Set<MarketProduct>().Include(x => x.Tags)
            .FirstAsync(x => x.Id == marketProductId);
        mp.Tags.Should().BeEmpty("the join row must be cleared when its tag is deleted");
    }

    // ── ON DELETE CASCADE from market_products → market_product_tags ──────────

    [Fact]
    public async Task HardDeleteMarketProduct_CascadesJoinRowRemovalAsync()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());
        var createResp = await _client.PostAsJsonAsync(
            Endpoint, new { name = $"cascade-{Guid.NewGuid().ToString("N")[..8]}", pinsToTop = false });
        var tag = (await createResp.Content.ReadFromJsonAsync<Envelope<TagBody>>())!.Data!;

        var marketId = await CreateMarketAsync($"IT-Mkt-Cascade-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Cascade-{Guid.NewGuid():N}", unitId);
        var marketProductId = await SeedMarketProductAsync(marketId, productId, 10_000m, 10, [tag.Id]);

        // Act — hard-delete the market_products row directly (app only ever soft-deletes;
        // this proves the FK's ON DELETE CASCADE annotation, not an app code path). If the join
        // table's market_product_id FK were NOT set to CASCADE, Postgres would reject this delete
        // with a foreign-key-violation DbUpdateException instead of auto-removing the join row.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mp = await db.Set<MarketProduct>().FirstAsync(x => x.Id == marketProductId);
        var act = async () =>
        {
            db.Remove(mp);
            await db.SaveChangesAsync();
        };

        // Assert
        await act.Should().NotThrowAsync("market_product_tags.market_product_id is ON DELETE CASCADE");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Admin-created restaurant user — bypasses the self-serve register/approval flow.</summary>
    private async Task<string> RegisterAndLoginRestaurantAsync()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());

        var email = $"tag-it-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = $"Tag IT Restaurant {Guid.NewGuid():N}"
        });
        create.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password });
        loginResp.EnsureSuccessStatusCode();
        var env = await loginResp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return env!.Data!.AccessToken;
    }

    private async Task<Guid> CreateMarketAsync(string name)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await LoginAsAdminAsync());
        var resp = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name,
            location = "Integration Zone",
            address = "1 Test St",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
    }

    private async Task<Guid> GetOrCreateUnitAsync(string unitName)
    {
        var listResp = await _client.GetAsync("/api/v1/units");
        listResp.EnsureSuccessStatusCode();
        var listEnv = await listResp.Content.ReadFromJsonAsync<Envelope<List<UnitBody>>>();
        var existing = listEnv!.Data?.FirstOrDefault(u =>
            u.Name.Equals(unitName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing.Id;

        var resp = await _client.PostAsJsonAsync("/api/v1/units", new { name = unitName });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
    }

    private async Task<Guid> CreateProductAsync(string name, Guid unitId)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name,
            unitId,
            categoryId = (Guid?)null,
            description = (string?)null
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
    }

    private async Task<Guid> SeedMarketProductAsync(
        Guid marketId, Guid productId, decimal price, int quantity, IReadOnlyList<Guid> tagIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mp = new MarketProduct(marketId, productId, price, quantity, null);
        var tags = await db.Set<Tag>().Where(t => tagIds.Contains(t.Id)).ToListAsync();
        mp.SetTags(tags, null);
        await db.Set<MarketProduct>().AddAsync(mp);
        await db.SaveChangesAsync();
        return mp.Id;
    }
}

public sealed record TagBody(Guid Id, string Name, bool PinsToTop);
