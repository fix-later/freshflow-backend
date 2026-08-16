using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetDeliveryAddresses;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDeliveryAddressesQueryHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly IDeliveryAddressRepository _addresses = Substitute.For<IDeliveryAddressRepository>();
    private readonly GetDeliveryAddressesQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public GetDeliveryAddressesQueryHandlerTests() =>
        _sut = new GetDeliveryAddressesQueryHandler(_restaurants, _addresses);

    private static RestaurantDto RestaurantFor(Guid userId) =>
        new(RestaurantId, "Test", RestaurantStatus.Active, DateTime.UtcNow, userId);

    [Fact]
    public async Task Handle_ValidQuery_ReturnsAddresses()
    {
        // Arrange
        IReadOnlyList<DeliveryAddressDto> list = new[]
        {
            new DeliveryAddressDto(Guid.NewGuid(), RestaurantId, "John", null,
                "Addr 1", null, null, true, DateTime.UtcNow, DateTime.UtcNow),
            new DeliveryAddressDto(Guid.NewGuid(), RestaurantId, null, null,
                "Addr 2", null, null, false, DateTime.UtcNow, DateTime.UtcNow)
        };
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.GetByRestaurantIdAsync(RestaurantId, default).Returns(list);

        // Act
        var result = await _sut.Handle(new GetDeliveryAddressesQuery(UserId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFoundError()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns((RestaurantDto?)null);

        // Act
        var result = await _sut.Handle(new GetDeliveryAddressesQuery(UserId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NoAddresses_ReturnsEmptyList()
    {
        // Arrange
        _restaurants.FindByUserIdAsync(UserId, default).Returns(RestaurantFor(UserId));
        _addresses.GetByRestaurantIdAsync(RestaurantId, default)
            .Returns(Array.Empty<DeliveryAddressDto>());

        // Act
        var result = await _sut.Handle(new GetDeliveryAddressesQuery(UserId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
