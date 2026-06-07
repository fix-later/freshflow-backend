using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetRoles;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetRolesQueryHandlerTests
{
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly GetRolesQueryHandler _sut;

    public GetRolesQueryHandlerTests() =>
        _sut = new GetRolesQueryHandler(_roles);

    [Fact]
    public async Task Handle_ReturnsAllRoles()
    {
        // Arrange
        var seeded = new List<Role>
        {
            new("admin", "Administrator"),
            new("driver", "Delivery driver"),
            new("hub_staff", "Hub staff"),
        };
        _roles.GetAllAsync(default).Returns(seeded);

        // Act
        var result = await _sut.Handle(new GetRolesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().Contain(r => r.Name == "admin");
        result.Value.Should().AllSatisfy(r => r.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_EmptyRolesTable_ReturnsEmptyList()
    {
        // Arrange
        _roles.GetAllAsync(default).Returns(new List<Role>());

        // Act
        var result = await _sut.Handle(new GetRolesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
