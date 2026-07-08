using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetCreditStatement;

internal sealed class GetCreditStatementQueryValidator : AbstractValidator<GetCreditStatementQuery>
{
    public GetCreditStatementQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.RestaurantId).NotEmpty();

        // Exactly one lookup mode: by StatementId, or by (Year, Month).
        RuleFor(q => q)
            .Must(q => q.StatementId.HasValue ^ (q.Year.HasValue && q.Month.HasValue))
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("Provide either 'statementId' or both 'year' and 'month', but not both.");

        RuleFor(q => q.Year)
            .Must(y => y!.Value is >= 2020 and <= 2100)
            .When(q => q.Year.HasValue)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'year' must be between 2020 and 2100.");

        RuleFor(q => q.Month)
            .Must(m => m!.Value is >= 1 and <= 12)
            .When(q => q.Month.HasValue)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'month' must be between 1 and 12.");
    }
}
