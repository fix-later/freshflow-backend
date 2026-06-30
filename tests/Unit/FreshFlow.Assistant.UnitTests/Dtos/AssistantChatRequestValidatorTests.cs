using FluentAssertions;
using FreshFlow.API.Assistant.Dtos;

namespace FreshFlow.Assistant.UnitTests.Dtos;

[Trait("Category", "Unit")]
public sealed class AssistantChatRequestValidatorTests
{
    private readonly AssistantChatRequestValidator _validator = new();

    private static AssistantChatRequest Valid() =>
        new(SessionId: Guid.NewGuid().ToString(), Message: "tìm cà chua giúp tôi", MarketId: Guid.NewGuid());

    [Fact]
    public void Passes_for_a_well_formed_request()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fails_when_session_id_is_empty(string sessionId)
    {
        var result = _validator.Validate(Valid() with { SessionId = sessionId });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssistantChatRequest.SessionId));
    }

    [Fact]
    public void Fails_when_session_id_exceeds_the_column_length()
    {
        // session_id maps to varchar(128) — an over-long id must be rejected at the boundary (400),
        // not blow up at INSERT time (500).
        var tooLong = new string('a', AssistantChatRequestValidator.MaxSessionIdLength + 1);
        var result = _validator.Validate(Valid() with { SessionId = tooLong });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssistantChatRequest.SessionId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fails_when_message_is_empty(string message)
    {
        var result = _validator.Validate(Valid() with { Message = message });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssistantChatRequest.Message));
    }

    [Fact]
    public void Fails_when_message_exceeds_the_cap()
    {
        var tooLong = new string('x', AssistantChatRequestValidator.MaxMessageLength + 1);
        var result = _validator.Validate(Valid() with { Message = tooLong });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AssistantChatRequest.Message));
    }
}
