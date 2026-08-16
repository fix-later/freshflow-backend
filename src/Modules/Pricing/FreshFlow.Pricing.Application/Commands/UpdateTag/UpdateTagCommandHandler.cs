using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.UpdateTag;

internal sealed class UpdateTagCommandHandler(ITagRepository tags)
    : IRequestHandler<UpdateTagCommand, Result<TagDto>>
{
    public async Task<Result<TagDto>> Handle(UpdateTagCommand request, CancellationToken ct)
    {
        var tag = await tags.FindByIdAsync(request.Id, ct);
        if (tag is null)
            return Result<TagDto>.Failure(Error.NotFound("TAG", request.Id));

        var normalizedName = Tag.NormalizeName(request.Name);
        var existing = await tags.FindByNameAsync(normalizedName, ct);
        if (existing is not null && existing.Id != tag.Id)
            return Result<TagDto>.Failure(
                Error.Conflict("TAG_NAME_CONFLICT", $"Tag '{normalizedName}' already exists."));

        tag.Rename(request.Name, request.Actor);
        tag.SetPinsToTop(request.PinsToTop, request.Actor);
        tags.Track(tag);
        await tags.SaveChangesAsync(ct);

        return Result<TagDto>.Success(new TagDto(tag.Id, tag.Name, tag.PinsToTop));
    }
}
