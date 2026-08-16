using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.CreateClaimProofUploadSignature;

public sealed record CreateClaimProofUploadSignatureCommand(
    Guid UserId,
    Guid OrderId) : ICommand<UploadSignatureResponse>;
