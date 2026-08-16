using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.AcknowledgeDiscrepancy;

public sealed record AcknowledgeDiscrepancyCommand(
    Guid HubId,
    Guid DiscrepancyId,
    Guid AdminUserId) : ICommand<HubDiscrepancyDto>;
