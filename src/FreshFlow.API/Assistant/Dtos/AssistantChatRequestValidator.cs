using FluentValidation;

namespace FreshFlow.API.Assistant.Dtos;

/// <summary>
/// Validates <see cref="AssistantChatRequest"/> (H1). The assistant controller invokes the orchestrator
/// directly rather than through <c>ISender.Send</c>, so the MediatR <c>ValidationBehavior</c> never runs
/// for this request — the controller must call this validator explicitly. <see cref="MaxSessionIdLength"/>
/// matches the <c>session_id varchar(128)</c> column so an over-long id is rejected with a 400 instead of
/// blowing up at INSERT/UPDATE time; <see cref="MaxMessageLength"/> caps prompt cost and bounds the
/// persisted conversation jsonb.
/// </summary>
public sealed class AssistantChatRequestValidator : AbstractValidator<AssistantChatRequest>
{
    public const int MaxSessionIdLength = 128;
    public const int MaxMessageLength = 4000;

    public AssistantChatRequestValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .MaximumLength(MaxSessionIdLength);

        RuleFor(x => x.Message)
            .NotEmpty()
            .MaximumLength(MaxMessageLength);
    }
}
