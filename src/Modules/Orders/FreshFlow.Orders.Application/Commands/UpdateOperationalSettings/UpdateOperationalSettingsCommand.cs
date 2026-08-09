using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.UpdateOperationalSettings;

public sealed record UpdateOperationalSettingsCommand(
    TimeOnly DailyCutoffTime,
    bool BatchingEnabled,
    string DefaultRouteType,
    int DeliveryWindowDays,
    decimal DeliveryFeePerKm = 5000m,
    decimal BaseFee = 0m,
    decimal MinimumFee = 0m,
    decimal RoundingUnit = 0m) : ICommand<OperationalSettingsDto>;
