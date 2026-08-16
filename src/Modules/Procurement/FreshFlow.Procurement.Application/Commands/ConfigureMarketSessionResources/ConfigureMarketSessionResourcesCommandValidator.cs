using FluentValidation;

namespace FreshFlow.Procurement.Application.Commands.ConfigureMarketSessionResources;

internal sealed class ConfigureMarketSessionResourcesCommandValidator
    : AbstractValidator<ConfigureMarketSessionResourcesCommand>
{
    public ConfigureMarketSessionResourcesCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.PlannedCapacityKg).GreaterThan(0m).LessThanOrEqualTo(1_000_000m);
        RuleFor(command => command.VehicleIds).NotEmpty();
        RuleFor(command => command.AgentUserIds).NotEmpty();
        RuleForEach(command => command.VehicleIds).NotEmpty();
        RuleForEach(command => command.AgentUserIds).NotEmpty();
    }
}
