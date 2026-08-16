using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.AcknowledgeDiscrepancy;

internal sealed class AcknowledgeDiscrepancyCommandValidator : AbstractValidator<AcknowledgeDiscrepancyCommand>
{
    public AcknowledgeDiscrepancyCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.DiscrepancyId).NotEmpty();
        RuleFor(x => x.AdminUserId).NotEmpty();
    }
}
