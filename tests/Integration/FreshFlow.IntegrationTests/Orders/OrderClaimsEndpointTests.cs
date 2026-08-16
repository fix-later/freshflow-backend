using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

[Trait("Category", "Integration")]
public sealed class OrderClaimsEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task Approve_ConcurrentRequests_RefundExactlyOnceAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var orderId = await SeedChargedAtHubOrderAsync(restaurant.RestaurantId);
        using var restaurantClient = Client(restaurant.Token);
        var filed = await restaurantClient.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/claims",
            new { amount = 50_000m, reason = "Damaged produce" });
        filed.StatusCode.Should().Be(HttpStatusCode.Created);
        var claimId = (await filed.Content.ReadFromJsonAsync<Envelope<ClaimBody>>())!.Data!.ClaimId;

        using var anonymous = factory.CreateClient();
        var adminToken = await LoginAsync(anonymous, "admin@test.freshflow", "AdminP@ss1");
        using var firstAdmin = Client(adminToken);
        using var secondAdmin = Client(adminToken);
        var endpoint = $"/api/v1/claims/{claimId}/approve";

        var responses = await Task.WhenAll(
            firstAdmin.PatchAsJsonAsync(endpoint, new { decisionNote = "Verified" }),
            secondAdmin.PatchAsJsonAsync(endpoint, new { decisionNote = "Verified" }));

        responses.Should().Contain(response => response.StatusCode == HttpStatusCode.OK);
        responses.Should().OnlyContain(response =>
            response.StatusCode == HttpStatusCode.OK
            || response.StatusCode == HttpStatusCode.Conflict);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var claim = await db.Set<OrderClaim>().AsNoTracking()
            .SingleAsync(value => value.Id == claimId);
        var account = await db.Set<RestaurantCredit>().AsNoTracking()
            .SingleAsync(value => value.RestaurantId == restaurant.RestaurantId);
        var refunds = await db.Set<CreditTransaction>().AsNoTracking()
            .Where(value =>
                value.OrderId == orderId
                && value.Type == CreditTransactionType.Refund)
            .ToListAsync();

        claim.Status.Should().Be(OrderClaimStatus.Approved);
        claim.RefundTransactionId.Should().Be(refunds.Single().Id);
        account.OutstandingBalance.Should().Be(50_000m);
        refunds.Single().Amount.Should().Be(50_000m);
    }

    private HttpClient Client(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<RestaurantIdentity> CreateRestaurantAsync()
    {
        using var client = factory.CreateClient();
        var adminToken = await LoginAsync(client, "admin@test.freshflow", "AdminP@ss1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"claims-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";

        (await client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = $"Claims Test {Guid.NewGuid():N}"
        })).EnsureSuccessStatusCode();

        var users = await client.GetFromJsonAsync<Envelope<UserListBody>>(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        var restaurantId = users!.Data!.Data.Single().RestaurantId!.Value;
        (await client.PatchAsync($"/api/v1/admin/restaurants/{restaurantId}/approve", null))
            .EnsureSuccessStatusCode();

        return new RestaurantIdentity(
            restaurantId,
            await LoginAsync(client, email, password));
    }

    private async Task<Guid> SeedChargedAtHubOrderAsync(Guid restaurantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        const string status = "AtHub";
        const string paymentStatus = "Outstanding";

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO orders (
                "Id", "RestaurantId", "Status", "PaymentStatus", "TotalAmount",
                "CreatedAt", "UpdatedAt", subtotal_amount, vat_amount, delivery_fee,
                delivery_distance_km)
            VALUES (
                {orderId}, {restaurantId}, {status}, {paymentStatus}, {100_000m},
                {now}, {now}, {100_000m}, {0m}, {0m}, {0m})
            """);

        var account = new RestaurantCredit(restaurantId, 200_000m);
        account.Charge(100_000m);
        db.Set<RestaurantCredit>().Add(account);
        db.Set<CreditTransaction>().Add(new CreditTransaction(
            restaurantId,
            orderId,
            CreditTransactionType.Charge,
            100_000m,
            100_000m,
            "Order confirmed"));
        await db.SaveChangesAsync();
        return orderId;
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string identifier,
        string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>())!.Data!.AccessToken;
    }

    private sealed record RestaurantIdentity(Guid RestaurantId, string Token);
    private sealed record ClaimBody(Guid ClaimId);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
}
