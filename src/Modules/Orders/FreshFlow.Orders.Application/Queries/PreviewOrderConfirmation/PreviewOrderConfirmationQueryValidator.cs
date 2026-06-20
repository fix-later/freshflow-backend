using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;

internal sealed class PreviewOrderConfirmationQueryValidator : AbstractValidator<PreviewOrderConfirmationQuery>
{
    public PreviewOrderConfirmationQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.OrderId).NotEmpty();
    }
}
