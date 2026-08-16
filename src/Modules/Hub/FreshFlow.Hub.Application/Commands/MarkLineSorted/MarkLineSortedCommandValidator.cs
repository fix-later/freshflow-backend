using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.MarkLineSorted;

internal sealed class MarkLineSortedCommandValidator : AbstractValidator<MarkLineSortedCommand>
{
    public MarkLineSortedCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.ServiceDate).NotEmpty();
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.SortedQuantityKg).GreaterThan(0);
    }
}
