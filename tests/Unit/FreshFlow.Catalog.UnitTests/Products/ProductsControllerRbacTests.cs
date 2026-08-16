using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace FreshFlow.Catalog.UnitTests.Products;

/// <summary>
/// Verifies that the ProductsController enforces admin-only RBAC on mutation endpoints,
/// explicitly blocking Market Agents (and all non-admin roles) from creating products
/// per GA-007 / FR-PRI-006.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProductsControllerRbacTests
{
    [Fact]
    public void CreateProduct_HasAdminOnlyAuthorization()
    {
        var method = typeof(ProductsController)
            .GetMethod(nameof(ProductsController.CreateProductAsync));

        method.Should().NotBeNull();

        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull("POST /products must require authorization");
        attr!.Roles.Should().Be("admin",
            "Market Agents and other non-admin roles must be blocked (403) from creating products");
    }

    [Fact]
    public void UpdateProduct_HasAdminOnlyAuthorization()
    {
        var method = typeof(ProductsController)
            .GetMethod(nameof(ProductsController.UpdateProductAsync));

        method.Should().NotBeNull();

        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin");
    }

    [Fact]
    public void DeactivateProduct_HasAdminOnlyAuthorization()
    {
        var method = typeof(ProductsController)
            .GetMethod(nameof(ProductsController.DeactivateProductAsync));

        method.Should().NotBeNull();

        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin");
    }
}
