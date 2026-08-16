using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.StartRoute;

internal sealed class StartRouteCommandValidator : AbstractValidator<StartRouteCommand>
{
    public StartRouteCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
    }
}
