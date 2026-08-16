using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.ListEligibleDrivers;

public sealed record ListEligibleDriversQuery(Guid HubId) : IRequest<Result<IReadOnlyList<DriverDto>>>;
