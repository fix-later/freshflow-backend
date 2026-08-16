using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Procurement;

[Trait("Category", "Integration")]
public sealed class ProcurementExceptionEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ExceptionFlow_ReportsProofPersistsAndExemptsUnavailableLineAsync()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);
        var marketId = await SeedMarketAsync();
        var owner = await CreateUserAccountAsync("market_agent", marketId);
        var otherAgent = await CreateUserAccountAsync("market_agent", marketId);
        var wrongRole = await CreateUserAccountAsync("hub_staff");
        var unavailableProductId = Guid.NewGuid();
        var purchasedProductId = Guid.NewGuid();
        var batchId = await SeedAssignedBatchAsync(
            marketId,
            owner.Id,
            unavailableProductId,
            purchasedProductId);

        var ownerToken = await LoginAsync(owner.Email, owner.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var signature = await _client.PostAsync(
            $"/api/v1/procurement/tasks/{batchId}/exceptions/upload-signature",
            null);

        signature.StatusCode.Should().Be(HttpStatusCode.OK);
        var signatureBody = await signature.Content
            .ReadFromJsonAsync<Envelope<UploadSignatureBody>>();
        signatureBody!.Data!.Signature.Should().NotBeNullOrWhiteSpace();
        signatureBody.Data.ApiKey.Should().Be("integration-test-key");
        signatureBody.Data.CloudName.Should().Be("integration-test-cloud");
        signatureBody.Data.Folder.Should().Be("freshflow/procurement-exceptions");

        const string proofUrl =
            "https://res.cloudinary.com/freshflow/image/upload/procurement-proof.jpg";
        var unavailableReport = await _client.PostAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/exceptions",
            new
            {
                marketProductId = unavailableProductId,
                type = "Unavailable",
                reportedQuantity = 2,
                note = "Supplier sold out",
                proofImageUrl = proofUrl
            });

        var unavailableError = await unavailableReport.Content.ReadAsStringAsync();
        unavailableReport.StatusCode.Should().Be(
            HttpStatusCode.Created,
            unavailableError);
        var unavailableBody = await unavailableReport.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        unavailableBody!.Data!.Exceptions.Should().ContainSingle(exception =>
            exception.MarketProductId == unavailableProductId &&
            exception.Type == "Unavailable" &&
            exception.ProofImageUrl == proofUrl);

        var shortfallReport = await _client.PostAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/exceptions",
            new
            {
                marketProductId = purchasedProductId,
                type = "Shortfall",
                reportedQuantity = 1,
                note = "Only part of the line was available",
                proofImageUrl = (string?)null
            });

        shortfallReport.StatusCode.Should().Be(HttpStatusCode.Created);
        var shortfallBody = await shortfallReport.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        shortfallBody!.Data!.Exceptions.Should().HaveCount(2);
        shortfallBody.Data.Exceptions.Should().Contain(exception =>
            exception.MarketProductId == purchasedProductId &&
            exception.Type == "Shortfall" &&
            exception.ProofImageUrl == null);

        var otherAgentToken = await LoginAsync(otherAgent.Email, otherAgent.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherAgentToken);
        var nonOwner = await _client.PostAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/exceptions",
            new
            {
                marketProductId = unavailableProductId,
                type = "Unavailable",
                reportedQuantity = 2,
                note = (string?)null,
                proofImageUrl = (string?)null
            });
        nonOwner.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var wrongRoleToken = await LoginAsync(wrongRole.Email, wrongRole.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", wrongRoleToken);
        var forbidden = await _client.PostAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/exceptions",
            new
            {
                marketProductId = unavailableProductId,
                type = "Unavailable",
                reportedQuantity = 2,
                note = (string?)null,
                proofImageUrl = (string?)null
            });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var purchase = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/purchase",
            new
            {
                lines = new[]
                {
                    new
                    {
                        marketProductId = purchasedProductId,
                        actualQuantity = 1,
                        actualUnitPrice = 11_000m
                    }
                }
            });

        purchase.StatusCode.Should().Be(HttpStatusCode.OK);
        var purchaseBody = await purchase.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        purchaseBody!.Data!.Status.Should().Be("Purchasing");
        purchaseBody.Data.Items.Single(item =>
                item.MarketProductId == unavailableProductId)
            .ActualQuantity.Should().BeNull();
        purchaseBody.Data.Items.Single(item =>
                item.MarketProductId == purchasedProductId)
            .ActualQuantity.Should().Be(1);
        await AssertPersistedAsync(batchId, unavailableProductId, purchasedProductId);
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

    private async Task<CreatedUser> CreateUserAccountAsync(
        string role,
        Guid? marketId = null)
    {
        var email = $"procurement-exception-{role}-{Guid.NewGuid():N}@test.freshflow";
        const string password = "UserP@ss1";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new { email, password, role, marketId });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<CreateUserBody>>();
        return new CreatedUser(body!.Data!.Id, email, password);
    }

    private async Task<Guid> SeedMarketAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var market = new Market(
            $"Exception Market {Guid.NewGuid():N}",
            "HCMC",
            "1 Test Street",
            null,
            null);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();
        return market.Id;
    }

    private async Task<Guid> SeedAssignedBatchAsync(
        Guid marketId,
        Guid agentUserId,
        Guid unavailableProductId,
        Guid purchasedProductId)
    {
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 16),
            marketId,
            [
                (unavailableProductId, "Unavailable Product", 2, Guid.NewGuid()),
                (purchasedProductId, "Purchased Product", 2, Guid.NewGuid())
            ])
            .Value;
        batch.Manifest(
            new Dictionary<Guid, decimal>
            {
                [unavailableProductId] = 10_000m,
                [purchasedProductId] = 10_000m
            },
            DateTime.UtcNow.AddHours(-2));
        batch.AssignItems(
            batch.Items.ToDictionary(item => item.MarketProductId, _ => agentUserId),
            DateTime.UtcNow.AddHours(-1));
        batch.ClearDomainEvents();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<ProcurementBatch>().Add(batch);
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private async Task AssertPersistedAsync(
        Guid batchId,
        Guid unavailableProductId,
        Guid purchasedProductId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .Include(candidate => candidate.Exceptions)
            .SingleAsync(candidate => candidate.Id == batchId);

        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        batch.Exceptions.Should().HaveCount(2);
        batch.Items.Single(item => item.MarketProductId == unavailableProductId)
            .ActualQuantity.Should().BeNull();
        batch.Items.Single(item => item.MarketProductId == purchasedProductId)
            .ActualQuantity.Should().Be(1);
    }

    private sealed record CreatedUser(Guid Id, string Email, string Password);
    private sealed record CreateUserBody(Guid Id);
    private sealed record UploadSignatureBody(
        string Signature,
        long Timestamp,
        string ApiKey,
        string CloudName,
        string Folder);
}
