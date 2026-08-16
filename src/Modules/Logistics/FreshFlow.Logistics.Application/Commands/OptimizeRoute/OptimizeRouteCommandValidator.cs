using FluentValidation;
using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.Application.Commands.OptimizeRoute;

internal sealed class OptimizeRouteCommandValidator : AbstractValidator<OptimizeRouteCommand>
{
    public OptimizeRouteCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();

        RuleFor(x => x.OptimizationCriteria)
            .NotEmpty()
            .Must(IsValidCriteria)
            .WithMessage("OptimizationCriteria must be DISTANCE, TIME, or COST.");
    }

    private static bool IsValidCriteria(string criteria) =>
        Enum.GetNames<OptimizationCriteria>()
            .Any(name => string.Equals(name, criteria, StringComparison.OrdinalIgnoreCase));
}
