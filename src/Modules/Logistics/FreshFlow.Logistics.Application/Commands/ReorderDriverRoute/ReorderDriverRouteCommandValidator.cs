using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.ReorderDriverRoute;

internal sealed class ReorderDriverRouteCommandValidator : AbstractValidator<ReorderDriverRouteCommand>
{
    public ReorderDriverRouteCommandValidator()
    {
        RuleFor(x => x.StopOrder).NotEmpty();
    }
}
