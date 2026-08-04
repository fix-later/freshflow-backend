using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace FreshFlow.Orders.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class ClaimsControllerRbacTests
{
    [Fact]
    public void ClaimsController_UsesSeededReadRoles()
    {
        var authorize = typeof(ClaimsController).GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("admin,operations_manager,restaurant");
    }

    [Theory]
    [InlineData(nameof(ClaimsController.ApproveAsync))]
    [InlineData(nameof(ClaimsController.RejectAsync))]
    public void ReviewActions_UseSeededPrivilegedRoles(string methodName)
    {
        var authorize = typeof(ClaimsController)
            .GetMethod(methodName)!
            .GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("admin,operations_manager");
    }

    [Fact]
    public void FileAction_UsesSeededRestaurantRole()
    {
        var authorize = typeof(ClaimsController)
            .GetMethod(nameof(ClaimsController.FileAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("restaurant");
    }
}
