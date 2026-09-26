using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Caching;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Queries.GetTags;

internal sealed class GetTagsQueryHandler(ITagRepository tags, PricingCache cache)
    : IRequestHandler<GetTagsQuery, Result<IReadOnlyList<TagDto>>>
{
    public async Task<Result<IReadOnlyList<TagDto>>> Handle(GetTagsQuery request, CancellationToken ct)
    {
        var dtos = await cache.GetTagsAsync(
            async factoryCt =>
            {
                var all = await tags.GetAllAsync(factoryCt);
                return (IReadOnlyList<TagDto>)all
                    .Select(t => new TagDto(t.Id, t.Name, t.PinsToTop)).ToList().AsReadOnly();
            },
            ct);

        return Result<IReadOnlyList<TagDto>>.Success(dtos);
    }
}
