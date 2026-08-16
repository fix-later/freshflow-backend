using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.CreateProofUploadSignature;

public sealed record CreateProofUploadSignatureCommand(
    Guid DeliveryId,
    Guid DriverUserId) : ICommand<UploadSignatureResponse>;
