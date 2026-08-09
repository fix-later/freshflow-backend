using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Operations;

[Trait("Category", "Integration")]
public sealed class OperationalProofEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private const string Password = "ProofP@ss1";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HubSignature_EnforcesRoleAssignmentAndInboundScopeAsync()
    {
        Authenticate(await LoginAsync("admin@test.freshflow", "AdminP@ss1"));
        var assignedStaff = await CreateUserAsync("hub_staff");
        var unassignedStaff = await CreateUserAsync("hub_staff");
        var (assignedHubId, assignedInboundId, otherHubId, otherInboundId) =
            await SeedHubInboundsAsync(assignedStaff.Id);

        Authenticate(await LoginAsync(assignedStaff.Email, Password));
        var valid = await _client.PostAsync(
            $"/api/v1/hubs/{assignedHubId}/inbound/{assignedInboundId}/discrepancy/upload-signature",
            null);
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        var signature = await valid.Content.ReadFromJsonAsync<Envelope<SignatureBody>>();
        signature!.Data!.Folder.Should().Be("freshflow/hub-discrepancies");

        var wrongHub = await _client.PostAsync(
            $"/api/v1/hubs/{otherHubId}/inbound/{otherInboundId}/discrepancy/upload-signature",
            null);
        wrongHub.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var wrongInbound = await _client.PostAsync(
            $"/api/v1/hubs/{assignedHubId}/inbound/{Guid.NewGuid()}/discrepancy/upload-signature",
            null);
        wrongInbound.StatusCode.Should().Be(HttpStatusCode.NotFound);

        Authenticate(await LoginAsync(unassignedStaff.Email, Password));
        (await _client.PostAsync(
            $"/api/v1/hubs/{assignedHubId}/inbound/{assignedInboundId}/discrepancy/upload-signature",
            null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Authenticate(await LoginAsync("admin@test.freshflow", "AdminP@ss1"));
        (await _client.PostAsync(
            $"/api/v1/hubs/{assignedHubId}/inbound/{assignedInboundId}/discrepancy/upload-signature",
            null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RouteDeliveries_ReturnsProofAndRestrictsOperationalReadersAsync()
    {
        Authenticate(await LoginAsync("admin@test.freshflow", "AdminP@ss1"));
        var operationsManager = await CreateUserAsync("operations_manager");
        var driver = await CreateUserAsync("driver");
        var hubStaff = await CreateUserAsync("hub_staff");
        var (routeId, emptyRouteId, proofUrl) = await SeedRoutesAsync();

        Authenticate(await LoginAsync(operationsManager.Email, Password));
        var response = await _client.GetAsync($"/api/v1/logistics/routes/{routeId}/deliveries");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<List<DriverDeliveryDto>>>();
        body!.Data!.Should().HaveCount(2);
        body.Data.Should().Contain(delivery => delivery.ProofUrl == proofUrl);
        body.Data.Should().Contain(delivery => delivery.ProofUrl == null);

        var empty = await _client.GetFromJsonAsync<Envelope<List<DriverDeliveryDto>>>(
            $"/api/v1/logistics/routes/{emptyRouteId}/deliveries");
        empty!.Data.Should().BeEmpty();
        (await _client.GetAsync(
            $"/api/v1/logistics/routes/{Guid.NewGuid()}/deliveries"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        Authenticate(await LoginAsync("admin@test.freshflow", "AdminP@ss1"));
        (await _client.GetAsync($"/api/v1/logistics/routes/{routeId}/deliveries"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Authenticate(await LoginAsync(driver.Email, Password));
        (await _client.GetAsync($"/api/v1/logistics/routes/{routeId}/deliveries"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Authenticate(await LoginAsync(hubStaff.Email, Password));
        (await _client.GetAsync($"/api/v1/logistics/routes/{routeId}/deliveries"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProcurementOrderGroups_OperationsManagerReadsProofButCannotWriteAsync()
    {
        Authenticate(await LoginAsync("admin@test.freshflow", "AdminP@ss1"));
        var operationsManager = await CreateUserAsync("operations_manager");
        var proofUrl = await SeedProcurementExceptionAsync();

        Authenticate(await LoginAsync(operationsManager.Email, Password));
        var response = await _client.GetAsync("/api/v1/admin/order-groups?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchListDto>>();
        body!.Data!.Batches.SelectMany(batch => batch.Exceptions)
            .Should().Contain(exception => exception.ProofImageUrl == proofUrl);

        var write = await _client.PostAsJsonAsync(
            "/api/v1/admin/order-groups/auto-batch",
            new
            {
                targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
                dryRun = true,
                force = false
            });
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Authenticate(await LoginAsync("admin@test.freshflow", "AdminP@ss1"));
        (await _client.GetAsync("/api/v1/admin/order-groups?pageSize=100"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(Guid AssignedHubId, Guid AssignedInboundId, Guid OtherHubId, Guid OtherInboundId)>
        SeedHubInboundsAsync(Guid assignedStaffId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignedHub = HubEntity.Create(
            $"Proof Hub A {Guid.NewGuid():N}", null, null, null, 1000m, null);
        var otherHub = HubEntity.Create(
            $"Proof Hub B {Guid.NewGuid():N}", null, null, null, 1000m, null);
        var assignedInbound = HubInboundEvent.Record(
            assignedHub.Id,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 1m)],
            DateTime.UtcNow);
        var otherInbound = HubInboundEvent.Record(
            otherHub.Id,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 1m)],
            DateTime.UtcNow);
        db.Set<HubEntity>().AddRange(assignedHub, otherHub);
        db.Set<HubInboundEvent>().AddRange(assignedInbound, otherInbound);
        db.Set<HubStaffAssignment>().Add(new HubStaffAssignment(assignedHub.Id, assignedStaffId));
        await db.SaveChangesAsync();

        return (assignedHub.Id, assignedInbound.Id, otherHub.Id, otherInbound.Id);
    }

    private async Task<(Guid RouteId, Guid EmptyRouteId, string ProofUrl)> SeedRoutesAsync()
    {
        var route = CreateRoute();
        var emptyRoute = CreateRoute();
        var first = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var second = Delivery.Create(route.Id, Guid.NewGuid(), 2);
        const string proofUrl = "https://res.cloudinary.com/demo/image/upload/pod-integration.jpg";
        second.AttachProof(proofUrl);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<DeliveryRoute>().AddRange(route, emptyRoute);
        db.Set<Delivery>().AddRange(first, second);
        await db.SaveChangesAsync();

        return (route.Id, emptyRoute.Id, proofUrl);
    }

    private static DeliveryRoute CreateRoute() =>
        DeliveryRoute.CreateDirect(
            DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
            [
                new RouteStop(
                    0, StopEntityType.market, Guid.NewGuid(), "Market",
                    10.1m, 106.1m, null, null),
                new RouteStop(
                    1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant",
                    10.2m, 106.2m, null, null)
            ],
            null);

    private async Task<string> SeedProcurementExceptionAsync()
    {
        var marketProductId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
            Guid.NewGuid(),
            [(marketProductId, "Proof product", 3, Guid.NewGuid())],
            Guid.NewGuid()).Value;
        var now = DateTime.UtcNow;
        batch.Manifest(new Dictionary<Guid, decimal> { [marketProductId] = 10_000m }, now)
            .IsSuccess.Should().BeTrue();
        const string proofUrl =
            "https://res.cloudinary.com/demo/image/upload/procurement-integration.jpg";
        batch.ReportException(
                marketProductId,
                ProcurementExceptionType.Damaged,
                1,
                "Damaged",
                proofUrl,
                Guid.NewGuid(),
                now)
            .IsSuccess.Should().BeTrue();
        batch.ClearDomainEvents();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<ProcurementBatch>().Add(batch);
        await db.SaveChangesAsync();

        return proofUrl;
    }

    private async Task<TestUser> CreateUserAsync(string role)
    {
        var email = $"proof-{role}-{Guid.NewGuid():N}@test.freshflow";
        if (role == "operations_manager")
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var roleEntity = await db.Set<Role>().SingleAsync(value => value.Name == role);
            var user = User.Create(email, hasher.Hash(Password), roleEntity);
            user.ClearDomainEvents();
            db.Set<User>().Add(user);
            await db.SaveChangesAsync();
            return new TestUser(user.Id, email);
        }

        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = Password,
            role
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<CreateUserBody>>();
        return new TestUser(body!.Data!.Id, email);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier,
            password
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return body!.Data!.AccessToken;
    }

    private void Authenticate(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private sealed record TestUser(Guid Id, string Email);
    private sealed record CreateUserBody(Guid Id);
    private sealed record SignatureBody(string Folder);
}
