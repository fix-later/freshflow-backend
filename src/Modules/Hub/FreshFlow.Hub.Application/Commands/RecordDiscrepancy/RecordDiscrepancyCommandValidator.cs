using FluentValidation;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Commands.RecordDiscrepancy;

internal sealed class RecordDiscrepancyCommandValidator : AbstractValidator<RecordDiscrepancyCommand>
{
    public RecordDiscrepancyCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.InboundEventId).NotEmpty();
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.AffectedQuantity).GreaterThan(0);
        RuleFor(x => x.ConditionStatus)
            .Must(status => status is HubDiscrepancy.ConditionMissing
                or HubDiscrepancy.ConditionDamaged
                or HubDiscrepancy.ConditionPartial);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
