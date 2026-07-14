using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.ConfirmPurchase;

internal sealed class ConfirmPurchaseCommandValidator : AbstractValidator<ConfirmPurchaseCommand>
{
    public ConfirmPurchaseCommandValidator()
    {
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.AgentUserId).NotEmpty();
        RuleFor(command => command.Lines).NotEmpty();
        RuleFor(command => command.Lines)
            .Must(lines => lines
                .Select(line => line.MarketProductId)
                .Distinct()
                .Count() == lines.Count)
            .When(command => command.Lines is { Count: > 0 })
            .WithMessage("Purchase lines must contain unique market product IDs.");

        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(value => value.MarketProductId).NotEmpty();
            line.RuleFor(value => value.ActualQuantity).GreaterThan(0);
            line.RuleFor(value => value.ActualUnitPrice).GreaterThan(0);
        });
    }
}
