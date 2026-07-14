using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.UpdateOperationalSettings;

internal sealed class UpdateOperationalSettingsCommandHandler(IOperationalSettingsRepository settings)
    : IRequestHandler<UpdateOperationalSettingsCommand, Result<OperationalSettingsDto>>
{
    public async Task<Result<OperationalSettingsDto>> Handle(
        UpdateOperationalSettingsCommand request, CancellationToken ct)
    {
        var updated = await settings.UpsertAsync(
            request.DailyCutoffTime, request.BatchingEnabled, request.DefaultRouteType, ct);

        return Result<OperationalSettingsDto>.Success(new OperationalSettingsDto(
            updated.DailyCutoffTime, updated.BatchingEnabled, updated.DefaultRouteType, updated.UpdatedAt));
    }
}
