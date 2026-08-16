using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Infrastructure.Persistence.Audit;
using FreshFlow.Infrastructure.Persistence.Entities;
using FreshFlow.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsRecentActivitiesEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecentActivities_ReturnsPagedNewestRowsAndAppliesActionAndEntityFiltersAsync()
    {
        await AuthenticateAsAdminAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var action = $"ana_recent_{suffix}";
        var otherAction = $"ana_other_{suffix}";
        var orderEntity = $"order_{suffix}";
        var routeEntity = $"route_{suffix}";
        var oldestId = Guid.NewGuid();
        var newestId = Guid.NewGuid();
        var middleId = Guid.NewGuid();
        var excludedId = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 7, 16, 7, 0, 0, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<AuditLog>().AddRange(
                NewAuditLog(oldestId, action, orderEntity, occurredAt),
                NewAuditLog(newestId, action, orderEntity, occurredAt.AddMinutes(2)),
                NewAuditLog(middleId, action, routeEntity, occurredAt.AddMinutes(1)),
                NewAuditLog(excludedId, otherAction, orderEntity, occurredAt.AddMinutes(3)));
            await db.SaveChangesAsync();
        }

        var firstResponse = await _client.GetAsync(
            $"{Endpoint}?action={Uri.EscapeDataString(action)}&page=1&pageSize=2");

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await firstResponse.Content.ReadFromJsonAsync<Envelope<AuditLogPageDto>>();
        first!.Data!.Page.Should().Be(1);
        first.Data.PageSize.Should().Be(2);
        first.Data.Total.Should().Be(3);
        first.Data.Data.Select(row => row.Id).Should().Equal(newestId, middleId);

        var secondResponse = await _client.GetAsync(
            $"{Endpoint}?action={Uri.EscapeDataString(action)}&page=2&pageSize=2");
        var second = await secondResponse.Content.ReadFromJsonAsync<Envelope<AuditLogPageDto>>();
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        second!.Data!.Data.Should().ContainSingle().Which.Id.Should().Be(oldestId);

        var filteredResponse = await _client.GetAsync(
            $"{Endpoint}?action={Uri.EscapeDataString(action)}" +
            $"&entityType={Uri.EscapeDataString(orderEntity)}");
        var filtered = await filteredResponse.Content.ReadFromJsonAsync<Envelope<AuditLogPageDto>>();
        filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        filtered!.Data!.Total.Should().Be(2);
        filtered.Data.Data.Select(row => row.Id).Should().Equal(newestId, oldestId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task RecentActivities_InvalidPageSize_ReturnsValidationErrorAsync(int pageSize)
    {
        await AuthenticateAsAdminAsync();

        var response = await _client.GetAsync($"{Endpoint}?pageSize={pageSize}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        body!.Error!.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task RecentActivities_WithRestaurantToken_Returns403Async()
    {
        await AuthenticateAsAdminAsync();
        var email = $"analytics-recent-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = "Recent Activities RBAC",
        });
        create.EnsureSuccessStatusCode();
        var token = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static AuditLog NewAuditLog(Guid id, string action, string entityType, DateTime occurredAt) =>
        new(id, null, action, entityType, Guid.NewGuid(), "{}", occurredAt, occurredAt);

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return body!.Data!.AccessToken;
    }

    private const string Endpoint = "/api/v1/analytics/recent-activities";
}
