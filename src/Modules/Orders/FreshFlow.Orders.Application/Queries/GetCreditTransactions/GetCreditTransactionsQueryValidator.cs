using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetCreditTransactions;

internal sealed class GetCreditTransactionsQueryValidator : AbstractValidator<GetCreditTransactionsQuery>
{
    public GetCreditTransactionsQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.RestaurantId).NotEmpty();
    }
}
