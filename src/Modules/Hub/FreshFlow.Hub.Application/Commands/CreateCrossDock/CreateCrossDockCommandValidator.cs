using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.CreateCrossDock;

internal sealed class CreateCrossDockCommandValidator : AbstractValidator<CreateCrossDockCommand>
{
    public CreateCrossDockCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.InboundEventId).NotEmpty();
        RuleFor(x => x.OutboundRouteId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
