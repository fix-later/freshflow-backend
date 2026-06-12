using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Queries.Products.GetProductById;
using FreshFlow.Catalog.Application.Queries.Products.GetProducts;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

/// <summary>
/// Tests for GET /api/v1/products/{id} (UC-CAT-03) — RBAC attributes and controller logic.
/// Also covers the IncludeInactive RBAC enforcement on GET /api/v1/products.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GetProductByIdControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    private ProductsController CreateController(string role)
    {
        var claims = new[] { new Claim(ClaimTypes.Role, role) };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);

        var controller = new ProductsController(_sender);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    // ── RBAC attribute tests ──────────────────────────────────────────────────

    [Fact]
    public void GetProductAsync_AllowsAllExpectedRoles()
    {
        var method = typeof(ProductsController)
            .GetMethod(nameof(ProductsController.GetProductAsync));

        method.Should().NotBeNull();

        var attr = method!.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        attr.Should().NotBeNull("GET /products/{id} must require authorization");

        var roles = attr!.Roles!.Split(',').Select(r => r.Trim()).ToHashSet();
        roles.Should().Contain("admin");
        roles.Should().Contain("operations_manager");
        roles.Should().Contain("market_agent");
        roles.Should().Contain("hub_staff");
        roles.Should().Contain("restaurant");

        roles.Should().NotContain("driver",
            because: "Drivers do not have access to the product catalog per RBAC matrix");
    }

    // ── Controller logic tests ────────────────────────────────────────────────

    [Fact]
    public async Task GetProductAsync_ProductFound_Returns200WithDto()
    {
        // Arrange
        var id = Guid.NewGuid();
        var dto = new ProductDto(id, "Cá lóc", null, null, Guid.NewGuid(), null, null, null,
            DateTime.UtcNow, DateTime.UtcNow, false);
        _sender.Send(Arg.Any<GetProductByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProductDto>.Success(dto));

        var sut = CreateController("market_agent");

        // Act
        var response = await sut.GetProductAsync(id, default);

        // Assert — response is wrapped in { success: true, data: dto } envelope
        response.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)response;
        ok.Value.Should().NotBeNull();
        var data = ok.Value!.GetType().GetProperty("data")?.GetValue(ok.Value);
        data.Should().NotBeNull("response envelope must include a 'data' property");
        data.Should().Be(dto);
        await _sender.Received(1).Send(
            Arg.Is<GetProductByIdQuery>(q => q.Id == id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProductAsync_ProductNotFound_Returns404()
    {
        // Arrange
        var id = Guid.NewGuid();
        _sender.Send(Arg.Any<GetProductByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProductDto>.Failure(Error.NotFound("Product", id)));

        var sut = CreateController("admin");

        // Act
        var response = await sut.GetProductAsync(id, default);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── IncludeInactive RBAC enforcement ─────────────────────────────────────

    [Theory]
    [InlineData("market_agent")]
    [InlineData("hub_staff")]
    [InlineData("restaurant")]
    public async Task GetProductsAsync_NonPrivilegedUser_ForcesIncludeInactiveFalse(string role)
    {
        // Arrange
        var emptyResponse = Result<GetProductsResponse>.Success(
            new GetProductsResponse([], new PaginationMeta(0, 1, 20)));
        _sender.Send(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(emptyResponse);

        var sut = CreateController(role);

        // Act — user requests IncludeInactive=true
        await sut.GetProductsAsync(new GetProductsQuery(null, null, true, 1, 20), default);

        // Assert — sender must have received a query with IncludeInactive forced to false
        await _sender.Received(1).Send(
            Arg.Is<GetProductsQuery>(q => !q.IncludeInactive),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("operations_manager")]
    public async Task GetProductsAsync_PrivilegedUser_HonorsIncludeInactiveTrue(string role)
    {
        // Arrange
        var emptyResponse = Result<GetProductsResponse>.Success(
            new GetProductsResponse([], new PaginationMeta(0, 1, 20)));
        _sender.Send(Arg.Any<GetProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(emptyResponse);

        var sut = CreateController(role);

        // Act — privileged user requests IncludeInactive=true
        await sut.GetProductsAsync(new GetProductsQuery(null, null, true, 1, 20), default);

        // Assert — sender must have received the original query unchanged
        await _sender.Received(1).Send(
            Arg.Is<GetProductsQuery>(q => q.IncludeInactive),
            Arg.Any<CancellationToken>());
    }
}
