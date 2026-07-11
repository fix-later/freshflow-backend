using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.RecordInbound;

internal sealed class RecordInboundCommandValidator : AbstractValidator<RecordInboundCommand>
{
    public RecordInboundCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.ArrivedAt).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.MarketProductId).NotEmpty();
            item.RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty)
                .When(x => x.ProductId.HasValue);
            item.RuleFor(x => x.QuantityKg).GreaterThan(0);
        });
    }
}
