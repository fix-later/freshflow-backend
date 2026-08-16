using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.ScanInbound;

public sealed record ScanInboundCommand(
    string Code,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false) : ICommand<HubInboundDto>;
