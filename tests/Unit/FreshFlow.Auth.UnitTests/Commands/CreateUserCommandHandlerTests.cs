using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.CreateUser;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateUserCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly IDriverProfileCreator _driverCreator = Substitute.For<IDriverProfileCreator>();
    private readonly IMarketValidator _marketValidator = Substitute.For<IMarketValidator>();
    private readonly CreateUserCommandHandler _sut;

    public CreateUserCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");
        _sut = new CreateUserCommandHandler(_users, _hasher, _restaurants, _driverCreator, _marketValidator);
    }

    [Theory]
    [InlineData("hub_staff")]
    [InlineData("driver")]
    public async Task Handle_ValidRole_CreatesUser(string role)
    {
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);

        var cmd = new CreateUserCommand("u@test.com", "P@ss1", role, null, null);
        var result = await _sut.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("u@test.com");
        result.Value.Role.Should().Be(role);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsConflict()
    {
        _users.ExistsAsync("dup@test.com", default).Returns(true);

        var result = await _sut.Handle(
            new CreateUserCommand("dup@test.com", "P@ss1", "driver", null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_InvalidMarketId_ReturnsInvalidMarket()
    {
        var marketId = Guid.NewGuid();
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _marketValidator.IsActiveMarketAsync(marketId, default).Returns(false);

        var result = await _sut.Handle(
            new CreateUserCommand("u@test.com", "P@ss1", "market_agent", marketId, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_MARKET");
    }

    [Fact]
    public async Task Handle_RestaurantRole_CreatesRestaurantProfile()
    {
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _restaurants.CreateAsync(Arg.Any<Guid>(), "Pho Ba Tu", default).Returns(Guid.NewGuid());

        var result = await _sut.Handle(
            new CreateUserCommand("r@test.com", "P@ss1", "restaurant", null, "Pho Ba Tu"), default);

        result.IsSuccess.Should().BeTrue();
        await _restaurants.Received(1).CreateAsync(Arg.Any<Guid>(), "Pho Ba Tu", default);
    }

    [Fact]
    public async Task Handle_KioskStaffAlias_NormalizedToMarketAgent()
    {
        var marketId = Guid.NewGuid();
        _users.ExistsAsync(Arg.Any<string>(), default).Returns(false);
        _marketValidator.IsActiveMarketAsync(marketId, default).Returns(true);

        var result = await _sut.Handle(
            new CreateUserCommand("k@test.com", "P@ss1", "kiosk_staff", marketId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("market_agent");
    }
}
