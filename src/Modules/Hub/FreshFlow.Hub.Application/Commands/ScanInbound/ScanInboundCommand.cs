using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.ScanInbound;

public sealed record ScanInboundCommand(string Code) : ICommand<HubInboundDto>;
