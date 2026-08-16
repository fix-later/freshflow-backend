using FluentValidation;
using FreshFlow.Orders.Application.Services;

namespace FreshFlow.Orders.Application.Commands.UpdateScheduledOrder;

internal sealed class UpdateScheduledOrderCommandValidator : AbstractValidator<UpdateScheduledOrderCommand>
{
    public UpdateScheduledOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ScheduledOrderId).NotEmpty();
        RuleFor(x => x.RecurrenceType)
            .Must(raw => raw is null || ScheduledOrderParsing.TryParseRecurrenceType(raw, out _))
            .WithMessage("RecurrenceType must be 'daily' or 'weekly'.");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.DeliveryAddressId)
            .NotEmpty()
            .When(x => x.DeliveryAddressId.HasValue);

        RuleFor(x => x.Items)
            .NotEmpty()
            .When(x => x.Items is not null)
            .WithMessage("A recurring schedule must have at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MarketProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
