using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsDeliveryPerformanceEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateOnly TargetDate = new(2026, 7, 16);
    private static readonly DateTime CreatedAt =
        new(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime At2330Vietnam =
        new(2026, 7, 16, 16, 30, 0, DateTimeKind.Utc);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DeliveryPerformance_ExecutesSeamsAndAppliesTimingSamplesAndSoftDeletesAsync()
    {
        await AuthenticateAsAdminAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hub = HubEntity.Create("Delivery Metrics Hub", null, null, null, 10_000m, null);
            db.Set<HubEntity>().Add(hub);
            await db.SaveChangesAsync();

            var vehicleA = Guid.NewGuid();
            var vehicleB = Guid.NewGuid();
            var vehicleC = Guid.NewGuid();
            var softDeletedVehicle = Guid.NewGuid();
            await InsertVehicleAsync(db, vehicleA, 100m);
            await InsertVehicleAsync(db, vehicleB, 200m);
            await InsertVehicleAsync(db, vehicleC, 100m);
            await InsertVehicleAsync(db, softDeletedVehicle, 100m, CreatedAt);

            var routeA = Guid.NewGuid();
            var routeNoHandover = Guid.NewGuid();
            var routeSoftHandover = Guid.NewGuid();
            var softDeletedRoute = Guid.NewGuid();
            var routeSoftVehicle = Guid.NewGuid();
            var routeSoftDelivery = Guid.NewGuid();
            var routeNextDay = Guid.NewGuid();
            await InsertRouteAsync(db, routeA, vehicleA);
            await InsertRouteAsync(db, routeNoHandover, vehicleB);
            await InsertRouteAsync(db, routeSoftHandover, vehicleC);
            await InsertRouteAsync(db, softDeletedRoute, vehicleA, CreatedAt);
            await InsertRouteAsync(db, routeSoftVehicle, softDeletedVehicle);
            await InsertRouteAsync(db, routeSoftDelivery, vehicleA);
            await InsertRouteAsync(db, routeNextDay, vehicleA);

            await InsertDeliveryAsync(
                db,
                routeA,
                "delivered",
                At2330Vietnam.AddMinutes(-14).AddSeconds(-59),
                At2330Vietnam);
            var lateActual = new DateTime(2026, 7, 16, 15, 0, 0, DateTimeKind.Utc);
            await InsertDeliveryAsync(
                db,
                routeA,
                "delivered",
                lateActual.AddMinutes(-15).AddSeconds(-1),
                lateActual);
            await InsertDeliveryAsync(
                db,
                routeNoHandover,
                "delivered",
                null,
                new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
            await InsertDeliveryAsync(
                db,
                routeSoftHandover,
                "delivered",
                new DateTime(2026, 7, 16, 10, 50, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 16, 11, 0, 0, DateTimeKind.Utc));
            await InsertDeliveryAsync(
                db,
                softDeletedRoute,
                "delivered",
                new DateTime(2026, 7, 16, 9, 50, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc));
            await InsertDeliveryAsync(
                db,
                routeSoftVehicle,
                "delivered",
                new DateTime(2026, 7, 16, 8, 50, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc));
            await InsertDeliveryAsync(
                db,
                routeSoftDelivery,
                "delivered",
                new DateTime(2026, 7, 16, 7, 50, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc),
                CreatedAt);
            await InsertDeliveryAsync(
                db,
                routeNextDay,
                "delivered",
                new DateTime(2026, 7, 16, 17, 20, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 16, 17, 30, 0, DateTimeKind.Utc));
            // Delivery.MarkFailed never sets actual_arrival, so a real failure has none —
            // it is only locatable in time via updated_at.
            await InsertDeliveryAsync(
                db,
                routeA,
                "failed",
                null,
                null,
                null,
                new DateTime(2026, 7, 16, 7, 0, 0, DateTimeKind.Utc));

            await InsertHandoverAsync(
                db,
                hub.Id,
                routeA,
                new DateTime(2026, 7, 16, 14, 0, 0, DateTimeKind.Utc));
            // Second CHECKED_OUT handover on the same route: the seam must collapse to the
            // earliest confirmation, not multiply every delivery on routeA.
            await InsertHandoverAsync(
                db,
                hub.Id,
                routeA,
                new DateTime(2026, 7, 16, 15, 0, 0, DateTimeKind.Utc));
            await InsertHandoverAsync(
                db,
                hub.Id,
                routeSoftHandover,
                new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc),
                CreatedAt);
            await InsertHandoverAsync(db, hub.Id, routeNoHandover, null);

            await InsertOutboundAsync(db, hub.Id, routeA, 150m);
            await InsertOutboundAsync(db, hub.Id, routeNoHandover, 40m);
            await InsertOutboundAsync(db, hub.Id, routeSoftHandover, 30m);
            await InsertOutboundAsync(db, hub.Id, softDeletedRoute, 100m);
            await InsertOutboundAsync(db, hub.Id, routeSoftVehicle, 100m);
            await InsertOutboundAsync(db, hub.Id, routeSoftDelivery, 100m);
            await InsertOutboundAsync(db, hub.Id, routeNextDay, 100m);
            await InsertOutboundAsync(db, hub.Id, routeA, 100m, CreatedAt);
        }

        var response = await _client.GetAsync(Endpoint());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<DeliveryPerformanceDto>>();
        body!.Data!.TotalDeliveries.Should().Be(6);
        body.Data.OnTimeCount.Should().Be(4);
        body.Data.LateCount.Should().Be(1);
        body.Data.OnTimeRatePercent.Should().Be(80m);
        body.Data.FailedCount.Should().Be(1);
        body.Data.AvgDeliveryDurationMinutes.Should().Be(105m);
        body.Data.DurationSampleCount.Should().Be(2);
        body.Data.AvgVehicleUtilizationPercent.Should().Be(50m);
        body.Data.UtilizationSampleCount.Should().Be(3);
    }

    [Fact]
    public async Task DeliveryPerformance_WithRestaurantToken_Returns403Async()
    {
        await AuthenticateAsAdminAsync();
        var user = await CreateRestaurantAsync();
        var token = await LoginAsync(user.Email, user.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Endpoint() =>
        $"/api/v1/analytics/delivery-performance?from={TargetDate:yyyy-MM-dd}" +
        $"&to={TargetDate:yyyy-MM-dd}";

    private static Task InsertVehicleAsync(
        AppDbContext db,
        Guid vehicleId,
        decimal capacityKg,
        DateTime? deletedAt = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO vehicles
                (id, plate_number, capacity_kg, vehicle_type, is_available,
                 created_at, updated_at, deleted_at)
            VALUES
                ({vehicleId}, {$"ANA-{vehicleId:N}"[..20]}, {capacityKg}, {"van"}, {true},
                 {CreatedAt}, {CreatedAt}, {deletedAt})
            """);

    private static Task InsertRouteAsync(
        AppDbContext db,
        Guid routeId,
        Guid? vehicleId,
        DateTime? deletedAt = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_routes
                (id, route_type, status, service_date, route_metadata, vehicle_id,
                 created_at, updated_at, deleted_at)
            VALUES
                ({routeId}, {"direct"}, {"completed"}, {TargetDate}, '[]'::jsonb, {vehicleId},
                 {CreatedAt}, {CreatedAt}, {deletedAt})
            """);

    private static Task InsertDeliveryAsync(
        AppDbContext db,
        Guid routeId,
        string status,
        DateTime? estimatedArrival,
        DateTime? actualArrival,
        DateTime? deletedAt = null,
        DateTime? updatedAt = null)
    {
        var deliveryId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO deliveries
                (id, delivery_route_id, order_id, sequence_number, status,
                 estimated_arrival, actual_arrival, created_at, updated_at, deleted_at)
            VALUES
                ({deliveryId}, {routeId}, {orderId}, {1}, {status},
                 {estimatedArrival}, {actualArrival}, {CreatedAt}, {updatedAt ?? CreatedAt},
                 {deletedAt})
            """);
    }

    private static Task InsertHandoverAsync(
        AppDbContext db,
        Guid hubId,
        Guid routeId,
        DateTime? driverConfirmedAt,
        DateTime? deletedAt = null)
    {
        var handoverId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var handedOverBy = Guid.NewGuid();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO hub_handover_events
                (id, hub_id, delivery_route_id, driver_user_id, status,
                 handed_over_by, handed_over_at, driver_confirmed_at,
                 created_at, updated_at, deleted_at)
            VALUES
                ({handoverId}, {hubId}, {routeId}, {driverId}, {"CHECKED_OUT"},
                 {handedOverBy}, {CreatedAt}, {driverConfirmedAt},
                 {CreatedAt}, {CreatedAt}, {deletedAt})
            """);
    }

    private static Task InsertOutboundAsync(
        AppDbContext db,
        Guid hubId,
        Guid routeId,
        decimal quantityKg,
        DateTime? deletedAt = null)
    {
        var outboundId = Guid.NewGuid();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO hub_outbound_events
                (id, hub_id, destination_route_id, items, total_quantity_kg,
                 dispatched_at, created_at, updated_at, deleted_at)
            VALUES
                ({outboundId}, {hubId}, {routeId}, '[]'::jsonb, {quantityKg},
                 {CreatedAt}, {CreatedAt}, {CreatedAt}, {deletedAt})
            """);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<UserCredentials> CreateRestaurantAsync()
    {
        var email = $"analytics-delivery-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = "Delivery Metrics Restaurant"
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
