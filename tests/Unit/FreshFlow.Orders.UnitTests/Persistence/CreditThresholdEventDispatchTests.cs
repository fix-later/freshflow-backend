using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;
using FreshFlow.Orders.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// End-to-end proof (real <see cref="AppDbContext"/> + <see cref="DomainEventDispatchInterceptor"/>,
/// not a minimal test DbContext) that a charge crossing a credit-limit threshold actually
/// dispatches <see cref="CreditLimitThresholdReachedDomainEvent"/> post-commit (SCRUM-266).
/// The interceptor's generic dispatch mechanism is already covered exhaustively for any
/// <c>AggregateRoot</c> in FreshFlow.Pricing.UnitTests.Persistence.DomainEventDispatchInterceptorTests
/// — this test only proves RestaurantCredit's specific wiring (EF model registration via
/// CreditRepository.Track, real ChargeAsync flow) actually reaches that mechanism.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreditThresholdEventDispatchTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();

    private static (CreditService Service, IPublisher Publisher) BuildSut(string dbName)
    {
        var publisher = Substitute.For<IPublisher>();
        var interceptor = new DomainEventDispatchInterceptor(publisher, NullLogger<DomainEventDispatchInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;
        var ctx = new AppDbContext(options);
        var creditRepository = new CreditRepository(ctx);

        var restaurantReader = Substitute.For<IRestaurantReader>();
        restaurantReader.FindByIdAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

        return (new CreditService(creditRepository, restaurantReader), publisher);
    }

    [Fact]
    public async Task ChargeAsync_CrossingWarningThreshold_DispatchesDomainEventPostCommitAsync()
    {
        var (service, publisher) = BuildSut($"db-{Guid.NewGuid()}");
        await service.SetCreditLimitAsync(RestaurantId, 100m, null, CancellationToken.None);

        var result = await service.ChargeAsync(RestaurantId, Guid.NewGuid(), 80m, "Order confirmed", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await publisher.Received(1).Publish(
            Arg.Is<CreditLimitThresholdReachedDomainEvent>(e =>
                e.RestaurantId == RestaurantId && e.Level == CreditAlertLevel.Warning),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeAsync_WithinAllThresholds_DoesNotDispatchDomainEventAsync()
    {
        var (service, publisher) = BuildSut($"db-{Guid.NewGuid()}");
        await service.SetCreditLimitAsync(RestaurantId, 100m, null, CancellationToken.None);

        var result = await service.ChargeAsync(RestaurantId, Guid.NewGuid(), 50m, "Order confirmed", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await publisher.DidNotReceive().Publish(
            Arg.Any<CreditLimitThresholdReachedDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChargeAsync_RepeatedChargeWithinSameLevel_DispatchesOnlyOnceAsync()
    {
        var (service, publisher) = BuildSut($"db-{Guid.NewGuid()}");
        await service.SetCreditLimitAsync(RestaurantId, 100m, null, CancellationToken.None);

        await service.ChargeAsync(RestaurantId, Guid.NewGuid(), 80m, "Order 1", CancellationToken.None); // crosses Warning
        await service.ChargeAsync(RestaurantId, Guid.NewGuid(), 10m, "Order 2", CancellationToken.None); // still Warning (90%)

        await publisher.Received(1).Publish(
            Arg.Any<CreditLimitThresholdReachedDomainEvent>(), Arg.Any<CancellationToken>());
    }
}
