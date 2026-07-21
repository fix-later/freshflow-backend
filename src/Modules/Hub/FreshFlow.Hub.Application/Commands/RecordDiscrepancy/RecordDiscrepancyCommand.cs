using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.RecordDiscrepancy;

public sealed record RecordDiscrepancyCommand(
    Guid HubId,
    Guid InboundEventId,
    Guid OrderItemId,
    decimal AffectedQuantity,
    string ConditionStatus,
    string? Notes,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : ICommand<HubDiscrepancyDto>, IHubAccessRequest;
