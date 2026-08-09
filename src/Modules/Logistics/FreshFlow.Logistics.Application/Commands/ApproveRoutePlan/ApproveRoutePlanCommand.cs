using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.ApproveRoutePlan;

public sealed record ApproveRoutePlanCommand(Guid PlanId) : ICommand<RoutePlanDto>;
