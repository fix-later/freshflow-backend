using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Queries.GetTags;

internal sealed class GetTagsQueryHandler(ITagRepository tags)
    : IRequestHandler<GetTagsQuery, Result<IReadOnlyList<TagDto>>>
{
    public async Task<Result<IReadOnlyList<TagDto>>> Handle(GetTagsQuery request, CancellationToken ct)
    {
        var all = await tags.GetAllAsync(ct);
        return Result<IReadOnlyList<TagDto>>.Success(
            all.Select(t => new TagDto(t.Id, t.Name, t.PinsToTop)).ToList().AsReadOnly());
    }
}
