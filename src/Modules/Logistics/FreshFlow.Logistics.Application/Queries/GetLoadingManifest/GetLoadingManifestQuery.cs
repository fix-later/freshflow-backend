using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.GetLoadingManifest;

public sealed record GetLoadingManifestQuery(Guid RouteId) : IQuery<LoadingManifestDto>;
