using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Controllers;
using Microsoft.AspNetCore.Http;

namespace FreshFlow.Assistant.UnitTests.Controllers;

public sealed class AssistantControllerTests
{
    [Theory]
    [InlineData(AssistantProviderFailure.AuthenticationFailed, StatusCodes.Status502BadGateway, "ASSISTANT_PROVIDER_AUTH_FAILED")]
    [InlineData(AssistantProviderFailure.RateLimited, StatusCodes.Status429TooManyRequests, "ASSISTANT_PROVIDER_RATE_LIMITED")]
    [InlineData(AssistantProviderFailure.Timeout, StatusCodes.Status504GatewayTimeout, "ASSISTANT_PROVIDER_TIMEOUT")]
    [InlineData(AssistantProviderFailure.Unavailable, StatusCodes.Status502BadGateway, "ASSISTANT_PROVIDER_UNAVAILABLE")]
    internal void ProviderFailure_returns_specific_error(
        AssistantProviderFailure failure,
        int expectedStatus,
        string expectedCode)
    {
        var result = AssistantController.ProviderFailure(failure);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));

        result.StatusCode.Should().Be(expectedStatus);
        body.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be(expectedCode);
    }
}
