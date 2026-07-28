using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.MarkLineSorted;

public sealed record MarkLineSortedCommand(
    Guid HubId,
    DateOnly ServiceDate,
    Guid OrderItemId,
    decimal SortedQuantityKg,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : ICommand<HubSortingProgressDto>, IHubAccessRequest;
