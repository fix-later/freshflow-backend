using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using FreshFlow.API.Assistant.Llm;

namespace FreshFlow.Assistant.UnitTests.Llm;

public class ZenMuxOptionsTests
{
    [Fact]
    public void Validate_returns_no_results_when_all_required_fields_are_set()
    {
        // Arrange
        var options = new ZenMuxOptions
        {
            BaseUrl = "https://zenmux.ai/api/v1",
            Model = "z-ai/glm-5.2-free",
            ApiKey = "sk-test-key",
            TimeoutSeconds = 60
        };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_fails_when_api_key_is_empty()
    {
        // Arrange — ApiKey loads from ENV (Assistant__ZenMux__ApiKey); fail fast when absent.
        var options = new ZenMuxOptions { ApiKey = string.Empty };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(ZenMuxOptions.ApiKey)));
    }

    [Fact]
    public void Validate_fails_when_base_url_is_empty()
    {
        // Arrange
        var options = new ZenMuxOptions { BaseUrl = string.Empty, ApiKey = "sk-test-key" };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(ZenMuxOptions.BaseUrl)));
    }

    [Fact]
    public void Validate_fails_when_model_is_empty()
    {
        // Arrange
        var options = new ZenMuxOptions { Model = string.Empty, ApiKey = "sk-test-key" };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(ZenMuxOptions.Model)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Validate_fails_when_timeout_seconds_is_out_of_range(int timeoutSeconds)
    {
        // Arrange
        var options = new ZenMuxOptions { ApiKey = "sk-test-key", TimeoutSeconds = timeoutSeconds };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(ZenMuxOptions.TimeoutSeconds)));
    }

    private static List<ValidationResult> Validate(ZenMuxOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
