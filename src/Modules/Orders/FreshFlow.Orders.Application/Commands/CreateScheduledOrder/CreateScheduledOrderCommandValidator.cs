using FluentValidation;
using FreshFlow.Orders.Application.Services;

namespace FreshFlow.Orders.Application.Commands.CreateScheduledOrder;

internal sealed class CreateScheduledOrderCommandValidator : AbstractValidator<CreateScheduledOrderCommand>
{
    public CreateScheduledOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RecurrenceType)
            .NotEmpty()
            .Must(raw => ScheduledOrderParsing.TryParseRecurrenceType(raw, out _))
            .WithMessage("RecurrenceType must be 'daily' or 'weekly'.");
        RuleFor(x => x.FirstRunAt).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.DeliveryAddressId).NotEmpty();

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("A recurring schedule must have at least one item.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MarketProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
