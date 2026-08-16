using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetCreditTransactions;

internal sealed class GetCreditTransactionsQueryValidator : AbstractValidator<GetCreditTransactionsQuery>
{
    public GetCreditTransactionsQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.RestaurantId).NotEmpty();
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);

        // from and to are validated separately for format (in the controller); here
        // we enforce logical consistency: from must be earlier than or equal to to.
        RuleFor(q => q)
            .Must(q => q.From is null || q.To is null || q.From.Value <= q.To.Value)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'from' must be earlier than or equal to 'to'.");
    }
}
