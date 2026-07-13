using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;

public sealed record AttachProofOfDeliveryCommand(
    Guid DeliveryId,
    Guid DriverUserId,
    string ProofUrl) : ICommand<AttachProofOfDeliveryResponse>;
