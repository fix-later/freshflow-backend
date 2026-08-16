using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using FreshFlow.API.Assistant.Llm;

namespace FreshFlow.Assistant.UnitTests.Llm;

public class GeminiOptionsTests
{
    [Fact]
    public void Validate_returns_no_results_when_all_required_fields_are_set()
    {
        // Arrange
        var options = new GeminiOptions
        {
            ProjectId = "freshflow-xxxxx",
            Location = "asia-southeast1",
            Model = "google/gemini-2.5-flash",
            TimeoutSeconds = 60
        };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_fails_when_project_id_is_empty()
    {
        // Arrange — ProjectId has no default; a missing GCP project must fail fast at boot.
        var options = new GeminiOptions { ProjectId = string.Empty };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(GeminiOptions.ProjectId)));
    }

    [Fact]
    public void Validate_fails_when_location_is_empty()
    {
        // Arrange
        var options = new GeminiOptions { ProjectId = "freshflow-xxxxx", Location = string.Empty };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(GeminiOptions.Location)));
    }

    [Fact]
    public void Validate_fails_when_model_is_empty()
    {
        // Arrange
        var options = new GeminiOptions { ProjectId = "freshflow-xxxxx", Model = string.Empty };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(GeminiOptions.Model)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public void Validate_fails_when_timeout_seconds_is_out_of_range(int timeoutSeconds)
    {
        // Arrange
        var options = new GeminiOptions { ProjectId = "freshflow-xxxxx", TimeoutSeconds = timeoutSeconds };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(GeminiOptions.TimeoutSeconds)));
    }

    [Fact]
    public void BuildEndpoint_composes_the_vertex_openai_compatible_url()
    {
        // Arrange
        var options = new GeminiOptions
        {
            ProjectId = "freshflow-xxxxx",
            Location = "asia-southeast1",
            Model = "google/gemini-2.5-flash"
        };

        // Act
        var endpoint = options.BuildEndpoint();

        // Assert
        endpoint.ToString().Should().Be(
            "https://asia-southeast1-aiplatform.googleapis.com/v1/projects/freshflow-xxxxx/locations/asia-southeast1/endpoints/openapi");
    }

    [Fact]
    public void BuildEndpoint_uses_the_unprefixed_host_for_the_global_location()
    {
        // Arrange — the "global" location has no region prefix; its host is aiplatform.googleapis.com.
        var options = new GeminiOptions
        {
            ProjectId = "freshflow-xxxxx",
            Location = "global",
            Model = "google/gemini-3.6-flash"
        };

        // Act
        var endpoint = options.BuildEndpoint();

        // Assert
        endpoint.ToString().Should().Be(
            "https://aiplatform.googleapis.com/v1/projects/freshflow-xxxxx/locations/global/endpoints/openapi");
    }

    private static List<ValidationResult> Validate(GeminiOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
