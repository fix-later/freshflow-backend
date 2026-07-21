using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.GenerateCreditStatement;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class GenerateCreditStatementCommandHandlerTests
{
    private readonly ICreditStatementGenerationService _generationService =
        Substitute.For<ICreditStatementGenerationService>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly GenerateCreditStatementCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    public GenerateCreditStatementCommandHandlerTests()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindByUserIdAsync(UserId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new GenerateCreditStatementCommandHandler(_generationService, _restaurantReader);
    }

    [Fact]
    public async Task Handle_RestaurantMissing_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default).Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.Handle(
            new GenerateCreditStatementCommand(UserId, IsAdmin: false, RestaurantId, 2026, 1), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
        await _generationService.DidNotReceive().GenerateAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RestaurantUserForDifferentRestaurant_ReturnsForbiddenAsync()
    {
        _restaurantReader.FindByIdAsync(OtherRestaurantId, default)
            .Returns(new RestaurantSnapshotDto(OtherRestaurantId, IsApproved: true));

        var result = await _sut.Handle(
            new GenerateCreditStatementCommand(UserId, IsAdmin: false, OtherRestaurantId, 2026, 1), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_AdminForAnyRestaurant_DelegatesToGenerationServiceAsync()
    {
        _restaurantReader.FindByIdAsync(OtherRestaurantId, default)
            .Returns(new RestaurantSnapshotDto(OtherRestaurantId, IsApproved: true));
        _generationService.GenerateAsync(OtherRestaurantId, 2026, 3, default)
            .Returns(Result<CreditStatementDto>.Success(
                new CreditStatementDto(Guid.NewGuid(), OtherRestaurantId, default, default, 0, 0, 0, 0, 0, default, default, [])));

        var result = await _sut.Handle(
            new GenerateCreditStatementCommand(UserId, IsAdmin: true, OtherRestaurantId, 2026, 3), default);

        result.IsSuccess.Should().BeTrue();
        await _generationService.Received(1).GenerateAsync(OtherRestaurantId, 2026, 3, default);
    }

    [Fact]
    public async Task Handle_OwningRestaurant_DelegatesToGenerationServiceAsync()
    {
        _generationService.GenerateAsync(RestaurantId, 2026, 5, default)
            .Returns(Result<CreditStatementDto>.Success(
                new CreditStatementDto(Guid.NewGuid(), RestaurantId, default, default, 0, 0, 0, 0, 0, default, default, [])));

        var result = await _sut.Handle(
            new GenerateCreditStatementCommand(UserId, IsAdmin: false, RestaurantId, 2026, 5), default);

        result.IsSuccess.Should().BeTrue();
        await _generationService.Received(1).GenerateAsync(RestaurantId, 2026, 5, default);
    }
}
