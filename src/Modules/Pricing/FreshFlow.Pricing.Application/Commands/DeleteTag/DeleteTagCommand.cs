using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.DeleteTag;

/// <summary>
/// DELETE /api/v1/tags/{id} — admin only. Soft-deletes the catalog tag and clears its
/// assignments (join rows) so it no longer appears on any market product listing.
/// </summary>
public sealed record DeleteTagCommand(Guid Id) : ICommand;
