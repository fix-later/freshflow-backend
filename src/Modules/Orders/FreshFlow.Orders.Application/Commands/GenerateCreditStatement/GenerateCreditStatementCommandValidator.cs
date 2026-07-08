using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.GenerateCreditStatement;

internal sealed class GenerateCreditStatementCommandValidator : AbstractValidator<GenerateCreditStatementCommand>
{
    public GenerateCreditStatementCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.RestaurantId).NotEmpty();
        RuleFor(c => c.Year).InclusiveBetween(2020, 2100);
        RuleFor(c => c.Month).InclusiveBetween(1, 12);
    }
}
