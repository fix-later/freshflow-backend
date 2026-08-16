using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetCreditTransactions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetCreditTransactionsQueryHandlerTests
{
    private readonly ICreditRepository _creditRepository = Substitute.For<ICreditRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly GetCreditTransactionsQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public GetCreditTransactionsQueryHandlerTests()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindByUserIdAsync(UserId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _creditRepository.GetTransactionsPageAsync(
                Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns((Array.Empty<CreditTransaction>(), (string?)null));
        _sut = new GetCreditTransactionsQueryHandler(_creditRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.Handle(new GetCreditTransactionsQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserForDifferentRestaurant_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByIdAsync(OtherRestaurantId, default)
            .Returns(new RestaurantSnapshotDto(OtherRestaurantId, IsApproved: true));

        var result = await _sut.Handle(new GetCreditTransactionsQuery(UserId, IsAdmin: false, OtherRestaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ReturnsTransactionsMappedToSnakeCaseTypeAsync()
    {
        var orderId = Guid.NewGuid();
        var transaction = new CreditTransaction(
            RestaurantId,
            orderId,
            CreditTransactionType.Charge,
            amount: 100m,
            balanceAfter: 100m,
            note: "Order confirmed");
        _creditRepository.GetTransactionsPageAsync(
                RestaurantId, null, 50, null, null, default)
            .Returns(([transaction], (string?)null));

        var result = await _sut.Handle(new GetCreditTransactionsQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().Type.Should().Be("charge");
        result.Value.Items.Single().OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Handle_PassesCursorPageSizeAndDateRangeToRepositoryAsync()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        await _sut.Handle(
            new GetCreditTransactionsQuery(UserId, IsAdmin: false, RestaurantId, "cursor-abc", 20, from, to),
            default);

        await _creditRepository.Received(1).GetTransactionsPageAsync(
            RestaurantId, "cursor-abc", 20, from, to, default);
    }

    [Fact]
    public async Task Handle_RepositoryReturnsNextCursor_ReturnsItInPageDtoAsync()
    {
        _creditRepository.GetTransactionsPageAsync(
                RestaurantId, null, 50, null, null, default)
            .Returns((Array.Empty<CreditTransaction>(), "next-cursor-xyz"));

        var result = await _sut.Handle(new GetCreditTransactionsQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.NextCursor.Should().Be("next-cursor-xyz");
        result.Value.PageSize.Should().Be(50);
    }
}
