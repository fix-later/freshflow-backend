using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetHub;

public sealed record GetHubQuery(Guid HubId) : IQuery<HubDto>;
