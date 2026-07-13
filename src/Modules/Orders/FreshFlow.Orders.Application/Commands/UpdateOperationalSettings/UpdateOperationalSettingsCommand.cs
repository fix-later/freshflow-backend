using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.UpdateOperationalSettings;

public sealed record UpdateOperationalSettingsCommand(
    TimeOnly DailyCutoffTime,
    bool BatchingEnabled,
    string DefaultRouteType) : ICommand<OperationalSettingsDto>;
