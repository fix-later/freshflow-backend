using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant.Tools;

namespace FreshFlow.Assistant.UnitTests.Tools;

[Trait("Category", "Unit")]
public sealed class AssistantToolRegistryTests
{
    private static readonly AssistantToolInvocationContext Ctx = new(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task InvokeAsync_dispatches_to_the_matching_tools_handler()
    {
        // Arrange
        var echoTool = new AssistantTool(
            "echo",
            "Echoes back its args.",
            EmptySchema(),
            Handler: (args, ctx, ct) => Task.FromResult(args.GetRawText()));
        var sut = new AssistantToolRegistry([echoTool]);
        var args = JsonSerializer.SerializeToElement(new { hello = "world" });

        // Act
        var result = await sut.InvokeAsync("echo", args, Ctx);

        // Assert
        result.Should().Contain("hello");
    }

    [Fact]
    public async Task InvokeAsync_returns_a_structured_error_for_an_unknown_tool_name()
    {
        // Arrange
        var sut = new AssistantToolRegistry([]);

        // Act
        var result = await sut.InvokeAsync("does_not_exist", EmptySchema(), Ctx);

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetProperty("error").GetString().Should().Be("UNKNOWN_TOOL");
    }

    [Fact]
    public async Task InvokeAsync_returns_a_structured_error_when_the_tool_has_no_handler()
    {
        // Arrange — mirrors the T1-era AssistantTool default (Handler = null) before T2 wires one in.
        var unwiredTool = new AssistantTool("unwired", "desc", EmptySchema());
        var sut = new AssistantToolRegistry([unwiredTool]);

        // Act
        var result = await sut.InvokeAsync("unwired", EmptySchema(), Ctx);

        // Assert
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        parsed.GetProperty("error").GetString().Should().Be("UNKNOWN_TOOL");
    }

    [Fact]
    public void Tools_exposes_every_registered_tool()
    {
        // Arrange
        var toolA = new AssistantTool("a", "desc", EmptySchema(), (_, _, _) => Task.FromResult("{}"));
        var toolB = new AssistantTool("b", "desc", EmptySchema(), (_, _, _) => Task.FromResult("{}"));

        // Act
        var sut = new AssistantToolRegistry([toolA, toolB]);

        // Assert
        sut.Tools.Should().BeEquivalentTo([toolA, toolB]);
    }

    private static JsonElement EmptySchema() =>
        JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });
}
