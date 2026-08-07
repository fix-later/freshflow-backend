using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.DeleteTag;

internal sealed class DeleteTagCommandHandler(ITagRepository tags)
    : IRequestHandler<DeleteTagCommand, Result>
{
    public async Task<Result> Handle(DeleteTagCommand request, CancellationToken ct)
    {
        var tag = await tags.FindByIdAsync(request.Id, ct);
        if (tag is null)
            return Result.Failure(Error.NotFound("TAG", request.Id));

        // Clear assignments first — RESTRICT on tag_id would otherwise be moot here (soft-delete
        // is an UPDATE, not a DELETE), but a deleted tag must stop appearing on any listing.
        await tags.ClearAssignmentsAsync(tag.Id, ct);

        tag.Delete();
        tags.Track(tag);
        await tags.SaveChangesAsync(ct);

        return Result.Success();
    }
}
