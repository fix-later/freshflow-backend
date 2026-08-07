using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.CreateTag;

internal sealed class CreateTagCommandHandler(ITagRepository tags)
    : IRequestHandler<CreateTagCommand, Result<TagDto>>
{
    public async Task<Result<TagDto>> Handle(CreateTagCommand request, CancellationToken ct)
    {
        var normalizedName = Tag.NormalizeName(request.Name);
        var existing = await tags.FindByNameAsync(normalizedName, ct);
        if (existing is not null)
            return Result<TagDto>.Failure(
                Error.Conflict("TAG_NAME_CONFLICT", $"Tag '{normalizedName}' already exists."));

        var tag = Tag.Create(request.Name, request.PinsToTop, request.Actor);
        await tags.AddAsync(tag, ct);
        await tags.SaveChangesAsync(ct);

        return Result<TagDto>.Success(new TagDto(tag.Id, tag.Name, tag.PinsToTop));
    }
}
