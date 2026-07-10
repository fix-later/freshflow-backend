using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.SelectRoute;

internal sealed class SelectRouteCommandValidator : AbstractValidator<SelectRouteCommand>
{
    public SelectRouteCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
    }
}
