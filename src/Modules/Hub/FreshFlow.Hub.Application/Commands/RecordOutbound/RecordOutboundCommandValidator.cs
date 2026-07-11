using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.RecordOutbound;

internal sealed class RecordOutboundCommandValidator : AbstractValidator<RecordOutboundCommand>
{
    public RecordOutboundCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.DestinationRouteId).NotEmpty();
        RuleFor(x => x.DispatchedAt).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.MarketProductId).NotEmpty();
            item.RuleFor(x => x.QuantityKg).GreaterThan(0m);
        });
    }
}
