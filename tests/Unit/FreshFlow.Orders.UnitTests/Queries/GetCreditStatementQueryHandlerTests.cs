using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetCreditStatement;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetCreditStatementQueryHandlerTests
{
    private readonly ICreditStatementRepository _statementRepository = Substitute.For<ICreditStatementRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly GetCreditStatementQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public GetCreditStatementQueryHandlerTests()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindByUserIdAsync(UserId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new GetCreditStatementQueryHandler(_statementRepository, _restaurantReader);
    }

    private static CreditStatement SampleStatement(Guid restaurantId) =>
        new(
            restaurantId,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            0m, 0m, 0m, 0m, []);

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default).Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.Handle(
            new GetCreditStatementQuery(UserId, IsAdmin: false, RestaurantId, StatementId: Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserForDifferentRestaurant_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByIdAsync(OtherRestaurantId, default)
            .Returns(new RestaurantSnapshotDto(OtherRestaurantId, IsApproved: true));

        var result = await _sut.Handle(
            new GetCreditStatementQuery(UserId, IsAdmin: false, OtherRestaurantId, StatementId: Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_StatementNotFound_ReturnsNotFoundAsync()
    {
        _statementRepository.FindByIdAsync(Arg.Any<Guid>(), default).Returns((CreditStatement?)null);

        var result = await _sut.Handle(
            new GetCreditStatementQuery(UserId, IsAdmin: false, RestaurantId, StatementId: Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDITSTATEMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_StatementBelongsToDifferentRestaurant_ReturnsNotFoundAsync()
    {
        var statementId = Guid.NewGuid();
        _statementRepository.FindByIdAsync(statementId, default).Returns(SampleStatement(OtherRestaurantId));

        var result = await _sut.Handle(
            new GetCreditStatementQuery(UserId, IsAdmin: false, RestaurantId, StatementId: statementId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDITSTATEMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ValidStatementId_ReturnsStatementAsync()
    {
        var statementId = Guid.NewGuid();
        var statement = SampleStatement(RestaurantId);
        _statementRepository.FindByIdAsync(statementId, default).Returns(statement);

        var result = await _sut.Handle(
            new GetCreditStatementQuery(UserId, IsAdmin: false, RestaurantId, StatementId: statementId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(statement.Id);
    }

    [Fact]
    public async Task Handle_LookupByPeriod_ResolvesPeriodStartAndQueriesRepositoryAsync()
    {
        var statement = SampleStatement(RestaurantId);
        _statementRepository.FindByPeriodAsync(RestaurantId, Arg.Any<DateTime>(), default).Returns(statement);

        var result = await _sut.Handle(
            new GetCreditStatementQuery(UserId, IsAdmin: false, RestaurantId, Year: 2026, Month: 6), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(statement.Id);
        await _statementRepository.Received(1).FindByPeriodAsync(RestaurantId, Arg.Any<DateTime>(), default);
    }
}
