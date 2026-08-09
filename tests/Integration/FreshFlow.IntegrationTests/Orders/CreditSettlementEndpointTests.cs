using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

[Trait("Category", "Integration")]
public sealed class CreditSettlementEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Settle_SameReferenceTwice_ReturnsConflictWithoutDoubleSettlementAsync()
    {
        var token = await LoginAsync();
        var restaurantId = await CreateRestaurantAsync(token);
        await SeedOutstandingBalanceAsync(restaurantId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var endpoint = $"/api/v1/admin/restaurants/{restaurantId}/credit/settle";
        var request = new
        {
            amount = 100m,
            paymentMethod = "bank_transfer",
            reference = " dev-bank-001 ",
            note = "Received in external payment flow"
        };

        var first = await _client.PostAsJsonAsync(endpoint, request);
        var duplicate = await _client.PostAsJsonAsync(endpoint, request);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await duplicate.Content.ReadFromJsonAsync<ErrorEnvelope>();
        error!.Error!.Code.Should().Be("CREDIT_SETTLEMENT_DUPLICATE_REFERENCE");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await db.Set<User>()
            .Where(user => user.Email == "admin@test.freshflow")
            .Select(user => user.Id)
            .SingleAsync();
        var settlements = await db.Set<CreditTransaction>()
            .Where(transaction =>
                transaction.RestaurantId == restaurantId &&
                transaction.Type == CreditTransactionType.Settlement)
            .ToListAsync();

        settlements.Should().ContainSingle();
        settlements[0].Reference.Should().Be("DEV-BANK-001");
        settlements[0].RecordedByUserId.Should().Be(adminId);
        (await db.Set<RestaurantCredit>().SingleAsync(credit => credit.RestaurantId == restaurantId))
            .OutstandingBalance.Should().Be(400m);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>())!.Data!.AccessToken;
    }

    private async Task<Guid> CreateRestaurantAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var email = $"settlement-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = "RestaurantP@ss1",
            role = "restaurant",
            restaurantName = $"Settlement Test {Guid.NewGuid():N}"
        });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private async Task SeedOutstandingBalanceAsync(Guid restaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var credit = new RestaurantCredit(restaurantId, 1_000m);
        credit.Charge(500m);
        db.Add(credit);
        db.Add(new CreditTransaction(
            restaurantId, null, CreditTransactionType.Charge, 500m, 500m, "Test charge"));
        await db.SaveChangesAsync();
    }

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
}
