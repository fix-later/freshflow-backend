using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;

public sealed record UpdateDeliveryStatusCommand(
    Guid DeliveryId,
    Guid DriverUserId,
    string Status,
    string? FailureReason) : ICommand<UpdateDeliveryStatusResponse>;
