using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

/// <summary>
/// ASSIST-E3-T5 — tests for GET /api/v1/orders/{orderId}/confirm-preview: RBAC attribute and
/// controller logic mapping <see cref="PreviewOrderConfirmationQuery"/> results to the API envelope.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PreviewOrderConfirmationControllerTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    private OrdersController CreateController(string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "test");
        var user = new ClaimsPrincipal(identity);

        var controller = new OrdersController(_sender);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    // ── RBAC attribute test ─────────────────────────────────────────────────

    [Fact]
    public void PreviewOrderConfirmationAsync_RestrictedToRestaurantRole()
    {
        var method = typeof(OrdersController)
            .GetMethod(nameof(OrdersController.PreviewOrderConfirmationAsync));

        method.Should().NotBeNull();

        var attr = method!.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull("GET /orders/{orderId}/confirm-preview must require authorization");
        attr!.Roles.Should().Be("restaurant");
    }

    // ── Controller logic tests ──────────────────────────────────────────────

    [Fact]
    public async Task PreviewOrderConfirmationAsync_Success_Returns200WithDto()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var dto = new OrderConfirmationPreviewDto(
            WouldSucceed: true,
            Issues: [],
            TotalAmount: 150_000m,
            ResolvedScheduledFor: DateTime.UtcNow.AddDays(1),
            RemainingCreditAfter: 850_000m);

        _sender.Send(Arg.Any<PreviewOrderConfirmationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderConfirmationPreviewDto>.Success(dto));

        var sut = CreateController("restaurant");

        // Act
        var response = await sut.PreviewOrderConfirmationAsync(orderId, default);

        // Assert — response is wrapped in { success: true, data: dto } envelope
        response.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)response;
        ok.Value.Should().NotBeNull();
        var dataProperty = ok.Value!.GetType().GetProperty("data");
        dataProperty.Should().NotBeNull();
        var data = dataProperty!.GetValue(ok.Value);
        data.Should().Be(dto);

        await _sender.Received(1).Send(
            Arg.Is<PreviewOrderConfirmationQuery>(q => q.OrderId == orderId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewOrderConfirmationAsync_WithIssues_Returns200WithIssueList()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var dto = new OrderConfirmationPreviewDto(
            WouldSucceed: false,
            Issues: [new PreviewIssueDto("ORDER_EMPTY", "Order has no items."),
                     new PreviewIssueDto("DELIVERY_DATE_OUT_OF_WINDOW", "Outside delivery window.")],
            TotalAmount: 0m,
            ResolvedScheduledFor: null,
            RemainingCreditAfter: null);

        _sender.Send(Arg.Any<PreviewOrderConfirmationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderConfirmationPreviewDto>.Success(dto));

        var sut = CreateController("restaurant");

        // Act
        var response = await sut.PreviewOrderConfirmationAsync(orderId, default);

        // Assert
        response.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)response;
        var dataProperty = ok.Value!.GetType().GetProperty("data");
        dataProperty.Should().NotBeNull();
        var data = dataProperty!.GetValue(ok.Value).Should().BeOfType<OrderConfirmationPreviewDto>().Subject;
        data.WouldSucceed.Should().BeFalse();
        data.Issues.Should().HaveCount(2);
    }

    [Fact]
    public async Task PreviewOrderConfirmationAsync_OrderNotFound_Returns404()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _sender.Send(Arg.Any<PreviewOrderConfirmationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderConfirmationPreviewDto>.Failure(Error.NotFound("Order", orderId)));

        var sut = CreateController("restaurant");

        // Act
        var response = await sut.PreviewOrderConfirmationAsync(orderId, default);

        // Assert
        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PreviewOrderConfirmationAsync_NotOwner_Returns403()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _sender.Send(Arg.Any<PreviewOrderConfirmationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderConfirmationPreviewDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant.")));

        var sut = CreateController("restaurant");

        // Act
        var response = await sut.PreviewOrderConfirmationAsync(orderId, default);

        // Assert
        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
