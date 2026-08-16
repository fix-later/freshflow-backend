using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Catalog;

/// <summary>
/// Regression test for SCRUM-126: GET /api/v1/products with no pagination params
/// must return 200 with meta { page: 1, pageSize: 20 } (documented defaults).
/// Previously the endpoint returned 400 VALIDATION_ERROR when page/pageSize were absent.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GetProductsDefaultPaginationTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _client = factory.CreateClient();

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

    [Fact]
    public async Task GetProducts_NoQueryParams_Returns200WithDefaultPaginationMeta()
    {
        // Arrange
        await AuthorizeAsAdminAsync();

        // Act — call with zero pagination query params
        var resp = await _client.GetAsync("/api/v1/products");

        // Assert
        resp.StatusCode.Should().Be(
            HttpStatusCode.OK,
            because: "page and pageSize are optional and default to 1 and 20 per docs/04-api-design.md");

        var env = await resp.Content.ReadFromJsonAsync<Envelope<DefaultPaginationBody>>(JsonOpts);
        env!.Success.Should().BeTrue();
        env.Data.Should().NotBeNull();
        env.Data!.Meta.Page.Should().Be(1);
        env.Data.Meta.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetProducts_OnlySearchParam_Returns200WithDefaultPaginationMeta()
    {
        // Arrange
        await AuthorizeAsAdminAsync();

        // Act — provide search but omit page/pageSize
        var resp = await _client.GetAsync("/api/v1/products?search=xyz");

        // Assert
        resp.StatusCode.Should().Be(
            HttpStatusCode.OK,
            because: "omitting page/pageSize should still succeed with defaults");

        var env = await resp.Content.ReadFromJsonAsync<Envelope<DefaultPaginationBody>>(JsonOpts);
        env!.Success.Should().BeTrue();
        env.Data!.Meta.Page.Should().Be(1);
        env.Data.Meta.PageSize.Should().Be(20);
    }

    // ── Nested private DTOs ───────────────────────────────────────────────────

    private sealed record DefaultPaginationBody(
        IReadOnlyList<object> Data,
        DefaultPaginationMeta Meta);

    private sealed record DefaultPaginationMeta(int Total, int Page, int PageSize);
}
