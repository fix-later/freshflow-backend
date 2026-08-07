using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Queries.GetTags;

/// <summary>GET /api/v1/tags — lists all live catalog tags.</summary>
public sealed record GetTagsQuery : IQuery<IReadOnlyList<TagDto>>;
