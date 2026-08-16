using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.ListCreditStatements;

internal sealed class ListCreditStatementsQueryValidator : AbstractValidator<ListCreditStatementsQuery>
{
    public ListCreditStatementsQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.RestaurantId).NotEmpty();
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
    }
}
