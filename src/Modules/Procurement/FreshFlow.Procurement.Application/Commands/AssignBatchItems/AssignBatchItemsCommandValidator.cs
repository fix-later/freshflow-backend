using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.AssignBatchItems;

internal sealed class AssignBatchItemsCommandValidator : AbstractValidator<AssignBatchItemsCommand>
{
    public AssignBatchItemsCommandValidator()
    {
        RuleFor(command => command.BatchId).NotEmpty();
        RuleFor(command => command.Assignments).NotNull();
        RuleFor(command => command.Assignments)
            .Must(assignments => assignments
                .Select(assignment => assignment.MarketProductId)
                .Distinct()
                .Count() == assignments.Count)
            .When(command => command.Assignments is not null)
            .WithMessage("Item assignments must contain unique market product IDs.");
        RuleForEach(command => command.Assignments).ChildRules(assignment =>
            assignment.RuleFor(value => value.MarketProductId).NotEmpty());
    }
}
