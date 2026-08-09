using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.CreateDiscrepancyProofUploadSignature;

public sealed record CreateDiscrepancyProofUploadSignatureCommand(
    Guid HubId,
    Guid InboundEventId,
    Guid ActorUserId,
    bool BypassHubAssignment = false)
    : ICommand<UploadSignatureResponse>, IHubAccessRequest;
