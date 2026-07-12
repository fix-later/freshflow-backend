using FluentValidation;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Commands.ReportDeliveryIssue;

internal sealed class ReportDeliveryIssueCommandValidator : AbstractValidator<ReportDeliveryIssueCommand>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        DeliveryIssue.TypeUndeliverable,
        DeliveryIssue.TypeDamaged,
        DeliveryIssue.TypeCustomerRejected,
        DeliveryIssue.TypeOther
    };

    public ReportDeliveryIssueCommandValidator()
    {
        RuleFor(x => x.DeliveryId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.IssueType)
            .NotEmpty()
            .Must(type => type is not null && AllowedTypes.Contains(type.Trim()))
            .WithMessage("IssueType must be undeliverable, damaged, customer_rejected, or other.");
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(DeliveryIssue.MaxDescriptionLength);
    }
}
