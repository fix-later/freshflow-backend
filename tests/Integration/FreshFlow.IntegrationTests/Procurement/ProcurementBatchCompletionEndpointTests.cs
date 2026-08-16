using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Procurement;

/// <summary>
/// Proves the DeliveryCompletedIntegrationEvent -> ProcurementBatch.MarkCompleted seam against real
/// Postgres. The keyless ConfirmedOrderRow view read (IConfirmedOrderReader.ReadStatusesAsync) does
/// not execute on the EF InMemory provider, so this can only be proven here.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProcurementBatchCompletionEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DeliveryCompleted_LastCoveredOrderSettles_CompletesBatchAsync()
    {
        var restaurantId = await CreateRestaurantAsync();
        var marketId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var firstOrderId = await SeedOrderAsync(restaurantId, OrderStatus.Delivering);
        var secondOrderId = await SeedOrderAsync(restaurantId, OrderStatus.Delivering);
        var batchId = await SeedHandedOffBatchAsync(marketId, hubId, firstOrderId, secondOrderId);

        await SetOrderStatusAsync(firstOrderId, OrderStatus.Delivered);
        await PublishDeliveryCompletedAsync(firstOrderId);

        await AssertBatchStatusAsync(batchId, ProcurementBatchStatus.HandedOff, expectCompletedAt: false);

        await SetOrderStatusAsync(secondOrderId, OrderStatus.Delivered);
        await PublishDeliveryCompletedAsync(secondOrderId);

        await AssertBatchStatusAsync(batchId, ProcurementBatchStatus.Completed, expectCompletedAt: true);
    }

    [Fact]
    public async Task DeliveryCompleted_OrderNotInAnyBatch_NoOpsWithoutThrowingAsync()
    {
        var restaurantId = await CreateRestaurantAsync();
        var orderId = await SeedOrderAsync(restaurantId, OrderStatus.Delivered);

        var act = () => PublishDeliveryCompletedAsync(orderId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadStatuses_SoftDeletedOrder_IsExcludedAsync()
    {
        var restaurantId = await CreateRestaurantAsync();
        var orderId = await SeedOrderAsync(restaurantId, OrderStatus.Confirmed);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Set<Order>().SingleAsync(candidate => candidate.Id == orderId);
        db.Entry(order).Property(nameof(Order.DeletedAt)).CurrentValue = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var statuses = await scope.ServiceProvider.GetRequiredService<IConfirmedOrderReader>()
            .ReadStatusesAsync([orderId], default);

        statuses.Should().NotContainKey(orderId);
    }

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"procurement-completion-{Guid.NewGuid():N}@test.freshflow";
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = "Procurement Completion Test Restaurant"
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();

        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
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

    private async Task<Guid> SeedOrderAsync(Guid restaurantId, OrderStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // No line items: the handler only reads Order.Status via the keyless orders view, and
        // order_items carries its own FK to market_products which this test has no need to seed.
        var order = new Order(restaurantId, DateTime.UtcNow.AddDays(1), null);
        order.ClearDomainEvents();
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        db.Entry(order).Property(nameof(Order.Status)).CurrentValue = status;
        await db.SaveChangesAsync();

        return order.Id;
    }

    private async Task SetOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Set<Order>().SingleAsync(candidate => candidate.Id == orderId);
        db.Entry(order).Property(nameof(Order.Status)).CurrentValue = status;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedHandedOffBatchAsync(
        Guid marketId,
        Guid hubId,
        Guid firstOrderId,
        Guid secondOrderId)
    {
        var build = ProcurementBatch.Build(
            DateOnly.FromDateTime(DateTime.UtcNow),
            marketId,
            [
                (Guid.NewGuid(), "Completion Test Tomato", 2, firstOrderId),
                (Guid.NewGuid(), "Completion Test Cabbage", 3, secondOrderId)
            ],
            hubId);
        build.IsSuccess.Should().BeTrue();
        build.Value.ClearDomainEvents();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<ProcurementBatch>().Add(build.Value);
        await db.SaveChangesAsync();

        db.Entry(build.Value).Property(nameof(ProcurementBatch.Status))
            .CurrentValue = ProcurementBatchStatus.HandedOff;
        await db.SaveChangesAsync();

        return build.Value.Id;
    }

    private async Task PublishDeliveryCompletedAsync(Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var now = DateTime.UtcNow;
        await publisher.Publish(
            new DeliveryCompletedIntegrationEvent(orderId, Guid.NewGuid(), now, now));
    }

    private async Task AssertBatchStatusAsync(
        Guid batchId,
        ProcurementBatchStatus expectedStatus,
        bool expectCompletedAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == batchId);

        batch.Status.Should().Be(expectedStatus);
        if (expectCompletedAt)
            batch.CompletedAt.Should().NotBeNull();
        else
            batch.CompletedAt.Should().BeNull();
    }

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);

    private sealed record UserSummaryBody(Guid Id, Guid? RestaurantId);
}
