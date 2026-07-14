using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.CreateExceptionProofUploadSignature;

public sealed record CreateExceptionProofUploadSignatureCommand(
    Guid BatchId,
    Guid AgentUserId) : ICommand<UploadSignatureResponse>;
