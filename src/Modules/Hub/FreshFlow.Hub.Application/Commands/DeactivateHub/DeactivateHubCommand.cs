using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.DeactivateHub;

public sealed record DeactivateHubCommand(Guid HubId) : ICommand<HubDto>;
