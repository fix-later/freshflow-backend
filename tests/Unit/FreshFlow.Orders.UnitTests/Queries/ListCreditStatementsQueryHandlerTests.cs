using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.ListCreditStatements;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListCreditStatementsQueryHandlerTests
{
    private readonly ICreditStatementRepository _statementRepository = Substitute.For<ICreditStatementRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly ListCreditStatementsQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public ListCreditStatementsQueryHandlerTests()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindByUserIdAsync(UserId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _statementRepository.GetPageAsync(
                Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<CreditStatement>(), (string?)null));
        _sut = new ListCreditStatementsQueryHandler(_statementRepository, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default).Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.Handle(new ListCreditStatementsQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserForDifferentRestaurant_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByIdAsync(OtherRestaurantId, default)
            .Returns(new RestaurantSnapshotDto(OtherRestaurantId, IsApproved: true));

        var result = await _sut.Handle(
            new ListCreditStatementsQuery(UserId, IsAdmin: false, OtherRestaurantId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ReturnsSummariesMappedFromStatementsAsync()
    {
        var statement = new CreditStatement(
            RestaurantId,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            0m, 100m, 0m, 0m, []);
        _statementRepository.GetPageAsync(RestaurantId, null, 50, default)
            .Returns(([statement], (string?)null));

        var result = await _sut.Handle(new ListCreditStatementsQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().Id.Should().Be(statement.Id);
        result.Value.Items.Single().ClosingBalance.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_PassesCursorAndPageSizeToRepositoryAsync()
    {
        await _sut.Handle(
            new ListCreditStatementsQuery(UserId, IsAdmin: false, RestaurantId, "cursor-abc", 20), default);

        await _statementRepository.Received(1).GetPageAsync(RestaurantId, "cursor-abc", 20, default);
    }

    [Fact]
    public async Task Handle_RepositoryReturnsNextCursor_ReturnsItInPageDtoAsync()
    {
        _statementRepository.GetPageAsync(RestaurantId, null, 50, default)
            .Returns((Array.Empty<CreditStatement>(), "next-cursor-xyz"));

        var result = await _sut.Handle(new ListCreditStatementsQuery(UserId, IsAdmin: false, RestaurantId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.NextCursor.Should().Be("next-cursor-xyz");
        result.Value.PageSize.Should().Be(50);
    }
}
