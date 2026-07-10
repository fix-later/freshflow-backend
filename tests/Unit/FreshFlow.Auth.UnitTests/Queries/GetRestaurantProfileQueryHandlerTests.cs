using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetRestaurantProfile;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetRestaurantProfileQueryHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly GetRestaurantProfileQueryHandler _sut;

    public GetRestaurantProfileQueryHandlerTests() =>
        _sut = new GetRestaurantProfileQueryHandler(_restaurants);

    private static RestaurantDto FullDto(RestaurantStatus status = RestaurantStatus.Active) =>
        new(
            Id: Guid.NewGuid(),
            Name: "Test Restaurant",
            Status: status,
            UpdatedAt: new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            UserId: Guid.NewGuid(),
            Address: "123 Market St",
            ContactPerson: "Jane Doe",
            PickupStart: new TimeOnly(8, 0),
            PickupEnd: new TimeOnly(20, 0),
            BusinessLicenseUrl: "https://res.cloudinary.com/demo/image/upload/v1/freshflow/licenses/abc.jpg");

    // ── NotFound ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoRestaurantLinked_ReturnsNotFoundError()
    {
        var userId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(userId, default).Returns((RestaurantDto?)null);

        var result = await _sut.Handle(new GetRestaurantProfileQuery(userId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    // ── Status lowercasing ────────────────────────────────────────────────────

    [Theory]
    [InlineData(RestaurantStatus.Pending, "pending")]
    [InlineData(RestaurantStatus.Active, "active")]
    [InlineData(RestaurantStatus.Suspended, "suspended")]
    public async Task Handle_AnyStatus_StatusIsLowercased(
        RestaurantStatus status, string expected)
    {
        var userId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(userId, default).Returns(FullDto(status));

        var result = await _sut.Handle(new GetRestaurantProfileQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(expected);
    }

    // ── Full field mapping ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RestaurantFound_MapsAllFields()
    {
        var userId = Guid.NewGuid();
        var dto = FullDto();
        _restaurants.FindByUserIdAsync(userId, default).Returns(dto);

        var result = await _sut.Handle(new GetRestaurantProfileQuery(userId), default);

        result.IsSuccess.Should().BeTrue();

        var response = result.Value;
        response.RestaurantId.Should().Be(dto.Id);
        response.Name.Should().Be(dto.Name);
        response.Status.Should().Be(dto.Status.ToString().ToLowerInvariant());
        response.Address.Should().Be(dto.Address);
        response.ContactPerson.Should().Be(dto.ContactPerson);
        response.PickupStart.Should().Be(dto.PickupStart);
        response.PickupEnd.Should().Be(dto.PickupEnd);
        response.UpdatedAt.Should().Be(dto.UpdatedAt);
        response.BusinessLicenseUrl.Should().Be(dto.BusinessLicenseUrl);
    }

    [Fact]
    public async Task Handle_RestaurantWithNullOptionals_MapsNullsCorrectly()
    {
        var userId = Guid.NewGuid();
        var dto = new RestaurantDto(
            Id: Guid.NewGuid(),
            Name: "Minimal Restaurant",
            Status: RestaurantStatus.Pending,
            UpdatedAt: DateTime.UtcNow,
            UserId: userId);
        _restaurants.FindByUserIdAsync(userId, default).Returns(dto);

        var result = await _sut.Handle(new GetRestaurantProfileQuery(userId), default);

        result.IsSuccess.Should().BeTrue();

        var response = result.Value;
        response.Address.Should().BeNull();
        response.ContactPerson.Should().BeNull();
        response.PickupStart.Should().BeNull();
        response.PickupEnd.Should().BeNull();
        response.BusinessLicenseUrl.Should().BeNull();
    }
}
