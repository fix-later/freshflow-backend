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
    }
}
