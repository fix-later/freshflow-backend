using FluentValidation;
using FreshFlow.Orders.Application.Queries;

namespace FreshFlow.Orders.Application.Commands.AdvanceOrderStatus;

internal sealed class AdvanceOrderStatusCommandValidator : AbstractValidator<AdvanceOrderStatusCommand>
{
    public AdvanceOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();

        RuleFor(x => x.Status)
            .Must(status =>
                OrderQueryParsing.TryParseStatus(status, out var parsed)
                && AdvanceOrderStatusCommandHandler.AllowedTargets.Contains(parsed))
            .WithMessage("Status must be one of: batched, picked_up, at_hub.");
    }
}
