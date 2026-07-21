using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Catalog;

[Trait("Category", "Integration")]
public sealed class CategoryHierarchyEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CategoryCrud_ReturnsParentIdAndFlatList()
    {
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var root = await CreateCategoryAsync($"Root-{tag}");
        var child = await CreateCategoryAsync($"Child-{tag}", root.Id);

        child.ParentId.Should().Be(root.Id);

        var getResponse = await _client.GetAsync($"/api/v1/categories/{child.Id}");
        getResponse.EnsureSuccessStatusCode();
        var getEnvelope = await getResponse.Content.ReadFromJsonAsync<Envelope<CategoryBody>>();
        getEnvelope!.Data!.ParentId.Should().Be(root.Id);

        var listResponse = await _client.GetAsync("/api/v1/categories");
        listResponse.EnsureSuccessStatusCode();
        var listEnvelope = await listResponse.Content
            .ReadFromJsonAsync<Envelope<IReadOnlyList<CategoryBody>>>();
        listEnvelope!.Data.Should().Contain(c => c.Id == root.Id && c.ParentId == null);
        listEnvelope.Data.Should().Contain(c => c.Id == child.Id && c.ParentId == root.Id);

        var otherRoot = await CreateCategoryAsync($"OtherRoot-{tag}");
        var reparentResponse = await _client.PutAsJsonAsync($"/api/v1/categories/{child.Id}", new
        {
            name = child.Name,
            parentId = (Guid?)otherRoot.Id
        });
        reparentResponse.EnsureSuccessStatusCode();
        var reparented = await reparentResponse.Content.ReadFromJsonAsync<Envelope<CategoryBody>>();
        reparented!.Data!.ParentId.Should().Be(otherRoot.Id);

        var promoteResponse = await _client.PutAsJsonAsync($"/api/v1/categories/{child.Id}", new
        {
            name = child.Name,
            parentId = (Guid?)null
        });
        promoteResponse.EnsureSuccessStatusCode();
        var promoted = await promoteResponse.Content.ReadFromJsonAsync<Envelope<CategoryBody>>();
        promoted!.Data!.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task HierarchyGuards_EnforceTwoLevelsAndActiveChildDeactivation()
    {
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var root = await CreateCategoryAsync($"GuardRoot-{tag}");
        var child = await CreateCategoryAsync($"GuardChild-{tag}", root.Id);
        var otherRoot = await CreateCategoryAsync($"GuardOther-{tag}");

        var thirdLevel = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"Grandchild-{tag}",
            parentId = (Guid?)child.Id
        });
        thirdLevel.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var missingParent = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"MissingParent-{tag}",
            parentId = (Guid?)Guid.NewGuid()
        });
        missingParent.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var selfParent = await _client.PutAsJsonAsync($"/api/v1/categories/{root.Id}", new
        {
            name = root.Name,
            parentId = (Guid?)root.Id
        });
        selfParent.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var activeChild = await _client.PatchAsync(
            $"/api/v1/categories/{root.Id}/deactivate", content: null);
        activeChild.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var deactivateChild = await _client.PatchAsync(
            $"/api/v1/categories/{child.Id}/deactivate", content: null);
        deactivateChild.EnsureSuccessStatusCode();

        var demoteWithInactiveChild = await _client.PutAsJsonAsync($"/api/v1/categories/{root.Id}", new
        {
            name = root.Name,
            parentId = (Guid?)otherRoot.Id
        });
        demoteWithInactiveChild.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var deactivateRoot = await _client.PatchAsync(
            $"/api/v1/categories/{root.Id}/deactivate", content: null);
        deactivateRoot.EnsureSuccessStatusCode();

        var inactiveParent = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"InactiveParent-{tag}",
            parentId = (Guid?)root.Id
        });
        inactiveParent.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ProductCategoryFilter_ExpandsParentAndKeepsChildExact()
    {
        await AuthorizeAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var root = await CreateCategoryAsync($"FilterRoot-{tag}");
        var child = await CreateCategoryAsync($"FilterChild-{tag}", root.Id);
        var sibling = await CreateCategoryAsync($"FilterSibling-{tag}", root.Id);
        var unrelated = await CreateCategoryAsync($"FilterOther-{tag}");
        var unitId = await CreateUnitAsync($"FilterUnit-{tag}");

        var rootProduct = await CreateProductAsync($"RootProduct-{tag}", unitId, root.Id);
        var childProduct = await CreateProductAsync($"ChildProduct-{tag}", unitId, child.Id);
        var siblingProduct = await CreateProductAsync($"SiblingProduct-{tag}", unitId, sibling.Id);
        var unrelatedProduct = await CreateProductAsync($"OtherProduct-{tag}", unitId, unrelated.Id);

        var parentPage = await GetProductsAsync(root.Id);
        parentPage.Data.Should().Contain(p => p.Id == rootProduct);
        parentPage.Data.Should().Contain(p => p.Id == childProduct);
        parentPage.Data.Should().Contain(p => p.Id == siblingProduct);
        parentPage.Data.Should().NotContain(p => p.Id == unrelatedProduct);

        var childPage = await GetProductsAsync(child.Id);
        childPage.Data.Should().ContainSingle(p => p.Id == childProduct);
        childPage.Data.Should().NotContain(p => p.Id == rootProduct || p.Id == siblingProduct);

        var missingPage = await GetProductsAsync(Guid.NewGuid());
        missingPage.Data.Should().BeEmpty();
        missingPage.Meta.Total.Should().Be(0);
    }

    private async Task AuthorizeAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", envelope!.Data!.AccessToken);
    }

    private async Task<CategoryBody> CreateCategoryAsync(string name, Guid? parentId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new { name, parentId });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<CategoryBody>>();
        return envelope!.Data!;
    }

    private async Task<Guid> CreateUnitAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/units", new
        {
            name,
            abbreviation = "u"
        });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return envelope!.Data!.Id;
    }

    private async Task<Guid> CreateProductAsync(string name, Guid unitId, Guid categoryId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name,
            unitId,
            categoryId,
            description = (string?)null
        });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return envelope!.Data!.Id;
    }

    private async Task<ProductPageBody> GetProductsAsync(Guid categoryId)
    {
        var response = await _client.GetAsync(
            $"/api/v1/products?category={categoryId}&page=1&pageSize=100");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<ProductPageBody>>();
        return envelope!.Data!;
    }

    private sealed record CategoryBody(Guid Id, string Name, Guid? ParentId, bool IsActive);
    private sealed record IdBody(Guid Id);
    private sealed record ProductPageBody(IReadOnlyList<ProductBody> Data, ProductMeta Meta);
    private sealed record ProductBody(Guid Id);
    private sealed record ProductMeta(int Total, int Page, int PageSize);
}
