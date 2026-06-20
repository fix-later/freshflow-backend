using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.ReorderFromHistory;

internal sealed class ReorderFromHistoryCommandValidator : AbstractValidator<ReorderFromHistoryCommand>
{
    public ReorderFromHistoryCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SourceOrderId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
