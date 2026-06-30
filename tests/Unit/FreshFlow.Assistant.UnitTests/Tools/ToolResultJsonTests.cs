using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant.Tools;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Assistant.UnitTests.Tools;

[Trait("Category", "Unit")]
public sealed class ToolResultJsonTests
{
    private sealed record SampleDto(string ProductName, decimal Price, decimal RemainingCreditAfter);

    [Fact]
    public void From_serializes_the_value_when_the_result_is_a_success()
    {
        // Arrange
        var result = Result<SampleDto>.Success(new SampleDto("Cà chua", 12000m, 500000m));

        // Act
        var json = ToolResultJson.From(result);

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        parsed.GetProperty("productName").GetString().Should().Be("Cà chua");
        parsed.GetProperty("price").GetDecimal().Should().Be(12000m);
    }

    [Fact]
    public void From_returns_a_structured_error_when_the_result_is_a_failure_instead_of_throwing()
    {
        // Arrange
        var result = Result<SampleDto>.Failure(new Error("CREDIT_LIMIT_EXCEEDED", "Vượt hạn mức công nợ."));

        // Act
        var json = ToolResultJson.From(result);

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        parsed.GetProperty("error").GetString().Should().Be("CREDIT_LIMIT_EXCEEDED");
        parsed.GetProperty("message").GetString().Should().Be("Vượt hạn mức công nợ.");
    }

    [Fact]
    public void From_strips_the_named_sensitive_field_from_the_success_payload()
    {
        // Arrange — RemainingCreditAfter must never reach the LLM prompt (design doc §2).
        var result = Result<SampleDto>.Success(new SampleDto("Cà chua", 12000m, 500000m));

        // Act
        var json = ToolResultJson.From(result, nameof(SampleDto.RemainingCreditAfter));

        // Assert
        json.Should().NotContain("RemainingCreditAfter", "stripped field must not leak under any casing")
            .And.NotContain("remainingCreditAfter");
        json.Should().NotContain("500000");
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        parsed.GetProperty("productName").GetString().Should().Be("Cà chua");
    }

    [Fact]
    public void From_does_not_strip_anything_from_a_failure_payload()
    {
        // Arrange — stripping only applies to success payloads; failures are already just code+message.
        var result = Result<SampleDto>.Failure(new Error("NOT_FOUND", "Order not found."));

        // Act
        var json = ToolResultJson.From(result, nameof(SampleDto.RemainingCreditAfter));

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        parsed.GetProperty("error").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Error_produces_a_code_and_message_pair()
    {
        // Act
        var json = ToolResultJson.Error("UNKNOWN_TOOL", "Tool 'foo' does not exist.");

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(json);
        parsed.GetProperty("error").GetString().Should().Be("UNKNOWN_TOOL");
        parsed.GetProperty("message").GetString().Should().Be("Tool 'foo' does not exist.");
    }
}
