using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.ReportOrderIssue;

internal sealed class ReportOrderIssueCommandValidator : AbstractValidator<ReportOrderIssueCommand>
{
    public ReportOrderIssueCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.OrderItemId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("OrderItemId must be null or a non-empty id.");
    }
}
