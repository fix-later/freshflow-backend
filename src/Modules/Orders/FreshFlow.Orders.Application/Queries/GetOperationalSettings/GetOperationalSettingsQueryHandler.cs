using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetOperationalSettings;

internal sealed class GetOperationalSettingsQueryHandler(IOperationalSettingsRepository settings)
    : IRequestHandler<GetOperationalSettingsQuery, Result<OperationalSettingsDto>>
{
    public async Task<Result<OperationalSettingsDto>> Handle(
        GetOperationalSettingsQuery request, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);
        return Result<OperationalSettingsDto>.Success(new OperationalSettingsDto(
            current.DailyCutoffTime,
            current.BatchingEnabled,
            current.DefaultRouteType,
            current.DeliveryWindowDays,
            current.DeliveryFeePerKm,
            current.UpdatedAt,
            current.BaseFee,
            current.MinimumFee,
            current.RoundingUnit));
    }
}
