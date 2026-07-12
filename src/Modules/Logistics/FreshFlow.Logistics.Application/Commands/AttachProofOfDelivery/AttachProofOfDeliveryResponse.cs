namespace FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;

public sealed record AttachProofOfDeliveryResponse(Guid DeliveryId, string ProofUrl);
