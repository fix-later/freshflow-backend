using FluentAssertions;
using FreshFlow.API.Extensions;
using FreshFlow.SharedKernel.Application;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.Hub.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class HubErrorExtensionsTests
{
    [Fact]
    public void ToActionResult_AlreadyReceived_Returns409()
    {
        var result = Error.Conflict("ALREADY_RECEIVED", "duplicate").ToActionResult();

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public void ToActionResult_HubCapacityExceeded_Returns422()
    {
        var result = Error.Validation("HUB_CAPACITY_EXCEEDED", "capacity").ToActionResult();

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public void ToActionResult_ScanNoMatch_Returns404()
    {
        var result = Error.Validation("SCAN_NO_MATCH", "no match").ToActionResult();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData("INSUFFICIENT_HUB_STOCK")]
    [InlineData("INBOUND_NOT_ARRIVED")]
    [InlineData("OUTBOUND_ROUTE_INVALID")]
    public void ToActionResult_HubDispatchValidationErrors_Return422(string code)
    {
        var result = Error.Validation(code, "dispatch").ToActionResult();

        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }
}
