using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace FreshFlow.Catalog.UnitTests.Products;

/// <summary>
/// Verifies that GET /products enforces role-based access per the RBAC matrix
/// (Admin, Ops, Agent, Hub, RMgr, RStaff = ✓; Driver, Public = ✗).
/// </summary>
[Trait("Category", "Unit")]
public sealed class GetProductsControllerRbacTests
{
    [Fact]
    public void GetProducts_AllowsExpectedRoles()
    {
        var method = typeof(ProductsController)
            .GetMethod(nameof(ProductsController.GetProductsAsync));

        method.Should().NotBeNull();

        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull("GET /products must require authorization");

        // All matrix-allowed roles must be present
        var roles = attr!.Roles!.Split(',').Select(r => r.Trim()).ToHashSet();
        roles.Should().Contain("admin");
        roles.Should().Contain("operations_manager");
        roles.Should().Contain("market_agent");
        roles.Should().Contain("hub_staff");
        roles.Should().Contain("restaurant");

        // Driver must NOT be in the allowed roles
        roles.Should().NotContain("driver",
            because: "Drivers do not have access to the product catalog per RBAC matrix");
    }
}
