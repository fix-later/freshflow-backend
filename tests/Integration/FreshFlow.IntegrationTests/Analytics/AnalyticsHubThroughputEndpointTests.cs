using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsHubThroughputEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateOnly TargetDate = new(2026, 7, 16);
    private static readonly DateTime At2330Vietnam =
        new(2026, 7, 16, 16, 30, 0, DateTimeKind.Utc);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HubThroughput_ExecutesSeamsAndAppliesVietnamDayHubAndSoftDeleteRulesAsync()
    {
        await AuthenticateAsAdminAsync();
        Guid hubAId;
        Guid hubBId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubA = HubEntity.Create("Alpha Hub", null, null, null, 1_000m, null);
            var hubB = HubEntity.Create("Beta Hub", null, null, null, 1_000m, null);
            var deletedHub = HubEntity.Create("Deleted Hub", null, null, null, 1_000m, null);
            hubAId = hubA.Id;
            hubBId = hubB.Id;
            db.Set<HubEntity>().AddRange(hubA, hubB, deletedHub);
            await db.SaveChangesAsync();

            var arrivedA = Inbound(hubA.Id, 10m, At2330Vietnam, arrived: true);
            var pendingA = Inbound(
                hubA.Id,
                100m,
                new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc),
                arrived: false);
            var arrivedB = Inbound(
                hubB.Id,
                20m,
                new DateTime(2026, 7, 16, 3, 0, 0, DateTimeKind.Utc),
                arrived: true);
            var nextVietnamDay = Inbound(
                hubA.Id,
                99m,
                new DateTime(2026, 7, 16, 17, 30, 0, DateTimeKind.Utc),
                arrived: true);
            var softDeletedInbound = Inbound(
                hubA.Id,
                50m,
                new DateTime(2026, 7, 16, 4, 0, 0, DateTimeKind.Utc),
                arrived: true);
            var deletedHubInbound = Inbound(
                deletedHub.Id,
                40m,
                new DateTime(2026, 7, 16, 5, 0, 0, DateTimeKind.Utc),
                arrived: true);
            db.Set<HubInboundEvent>().AddRange(
                arrivedA,
                pendingA,
                arrivedB,
                nextVietnamDay,
                softDeletedInbound,
                deletedHubInbound);

            var outboundA = Outbound(
                hubA.Id,
                15m,
                new DateTime(2026, 7, 16, 16, 0, 0, DateTimeKind.Utc));
            var outboundB = Outbound(
                hubB.Id,
                5m,
                new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc));
            var nextVietnamDayOutbound = Outbound(
                hubA.Id,
                99m,
                new DateTime(2026, 7, 16, 17, 30, 0, DateTimeKind.Utc));
            var softDeletedOutbound = Outbound(
                hubB.Id,
                50m,
                new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc));
            var deletedHubOutbound = Outbound(
                deletedHub.Id,
                40m,
                new DateTime(2026, 7, 16, 11, 0, 0, DateTimeKind.Utc));
            db.Set<HubOutboundEvent>().AddRange(
                outboundA,
                outboundB,
                nextVietnamDayOutbound,
                softDeletedOutbound,
                deletedHubOutbound);
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE hub_inbound_events SET deleted_at = {At2330Vietnam} WHERE id = {softDeletedInbound.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE hub_outbound_events SET deleted_at = {At2330Vietnam} WHERE id = {softDeletedOutbound.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE hubs SET deleted_at = {At2330Vietnam} WHERE id = {deletedHub.Id}");
        }

        var metrics = await ReadMetricsAsync(null);

        metrics.Summary.InboundKg.Should().Be(30m);
        metrics.Summary.OutboundKg.Should().Be(20m);
        metrics.Summary.NetKg.Should().Be(10m);
        metrics.Summary.InboundEventCount.Should().Be(3);
        metrics.Summary.OutboundEventCount.Should().Be(2);
        metrics.Summary.InboundStatusCounts.Should().Contain(new Dictionary<string, int>
        {
            ["PENDING"] = 1,
            ["ARRIVED_AT_HUB"] = 2
        });
        metrics.Buckets.Should().Equal(
            new HubThroughputBucketDto(TargetDate, hubAId, "Alpha Hub", 10m, 15m, 2, 1),
            new HubThroughputBucketDto(
                TargetDate,
                hubBId,
                "Beta Hub",
                20m,
                5m,
                1,
                1));

        var hubMetrics = await ReadMetricsAsync(hubAId);

        hubMetrics.Summary.InboundKg.Should().Be(10m);
        hubMetrics.Summary.OutboundKg.Should().Be(15m);
        hubMetrics.Summary.NetKg.Should().Be(-5m);
        hubMetrics.Summary.InboundEventCount.Should().Be(2);
        hubMetrics.Summary.OutboundEventCount.Should().Be(1);
        hubMetrics.Buckets.Should().ContainSingle().Which.HubId.Should().Be(hubAId);
    }

    [Fact]
    public async Task HubThroughput_WithHubStaffToken_Returns200Async()
    {
        await AuthenticateAsAdminAsync();
        var user = await CreateUserAsync("hub_staff");
        var token = await LoginAsync(user.Email, user.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint(null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HubThroughput_WithRestaurantToken_Returns403Async()
    {
        await AuthenticateAsAdminAsync();
        var user = await CreateUserAsync("restaurant");
        var token = await LoginAsync(user.Email, user.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint(null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HubThroughputDto> ReadMetricsAsync(Guid? hubId)
    {
        var response = await _client.GetAsync(Endpoint(hubId));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<HubThroughputDto>>();
        return body!.Data!;
    }

    private static string Endpoint(Guid? hubId) =>
        $"/api/v1/analytics/hub-throughput?from={TargetDate:yyyy-MM-dd}" +
        $"&to={TargetDate:yyyy-MM-dd}" +
        (hubId is null ? string.Empty : $"&hubId={hubId}");

    private static HubInboundEvent Inbound(
        Guid hubId,
        decimal kg,
        DateTime arrivedAt,
        bool arrived)
    {
        var inbound = HubInboundEvent.Record(
            hubId,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, kg)],
            arrivedAt);
        if (arrived)
        {
            inbound.ConfirmArrival();
        }

        return inbound;
    }

    private static HubOutboundEvent Outbound(Guid hubId, decimal kg, DateTime dispatchedAt) =>
        HubOutboundEvent.Record(
            hubId,
            Guid.NewGuid(),
            [new HubOutboundItem(Guid.NewGuid(), null, kg)],
            dispatchedAt);

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<UserCredentials> CreateUserAsync(string role)
    {
        var email = $"analytics-throughput-{role}-{Guid.NewGuid():N}@test.freshflow";
        const string password = "UserP@ss1";
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role,
            restaurantName = role == "restaurant" ? "Throughput Test Restaurant" : null
        });
        response.EnsureSuccessStatusCode();
        return new UserCredentials(email, password);
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

    private sealed record UserCredentials(string Email, string Password);
}
