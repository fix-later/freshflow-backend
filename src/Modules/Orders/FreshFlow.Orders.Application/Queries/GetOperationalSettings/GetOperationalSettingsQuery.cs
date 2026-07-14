using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetOperationalSettings;

public sealed record GetOperationalSettingsQuery : IQuery<OperationalSettingsDto>;
