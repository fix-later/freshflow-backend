using FluentAssertions;
using FreshFlow.API.Extensions;
using FreshFlow.SharedKernel.Application;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.Auth.UnitTests.Extensions;

[Trait("Category", "Unit")]
public sealed class ErrorExtensionsTests
{
    [Theory]
    [InlineData("EMAIL_ALREADY_EXISTS")]
    [InlineData("PHONE_ALREADY_EXISTS")]
    [InlineData("REFRESH_TOKEN_REUSE")]
    [InlineData("ALREADY_APPROVED")]
    public void ToActionResult_ConflictCodes_Returns409(string code)
    {
        var error = new Error(code, "conflict message");

        var result = error.ToActionResult();

        result.Should().BeOfType<ConflictObjectResult>();
        ((ConflictObjectResult)result).StatusCode.Should().Be(409);
    }

    [Theory]
    [InlineData("USER_NOT_FOUND")]
    [InlineData("RESTAURANT_NOT_FOUND")]
    public void ToActionResult_NotFoundCodes_Returns404(string code)
    {
        var error = new Error(code, "not found");

        var result = error.ToActionResult();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData("VALIDATION_ERROR")]
    [InlineData("WEAK_PASSWORD")]
    [InlineData("INVALID_ROLE")]
    public void ToActionResult_ValidationCodes_Returns400(string code)
    {
        var error = new Error(code, "bad request");

        var result = error.ToActionResult();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void ToActionResult_RoleNotConfigured_Returns500()
    {
        var error = new Error("ROLE_NOT_CONFIGURED", "system config error");

        var result = (ObjectResult)error.ToActionResult();

        result.StatusCode.Should().Be(500);
    }

    [Theory]
    [InlineData("INVALID_CREDENTIALS")]
    [InlineData("TOKEN_INVALID")]
    [InlineData("UNAUTHORIZED")]
    [InlineData("REFRESH_TOKEN_EXPIRED")]
    public void ToActionResult_UnauthorizedCodes_Returns401(string code)
    {
        var error = new Error(code, "unauthorized");

        var result = error.ToActionResult();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Theory]
    [InlineData("INVALID_CREDIT_LIMIT")]
    [InlineData("CREDIT_LIMIT_BELOW_OUTSTANDING_BALANCE")]
    [InlineData("DELIVERY_DATE_OUT_OF_WINDOW")]
    public void ToActionResult_CreditLimitValidationCodes_Returns422(string code)
    {
        var error = new Error(code, "validation error");

        var result = error.ToActionResult();

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void ToActionResult_OrderCannotReschedule_Returns409()
    {
        var error = new Error("ORDER_CANNOT_RESCHEDULE", "conflict message");

        var result = error.ToActionResult();

        result.Should().BeOfType<ConflictObjectResult>();
    }
}
