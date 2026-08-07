using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.UpdateTag;

/// <summary>PUT /api/v1/tags/{id} — admin only. Renames and/or toggles the pin flag.</summary>
public sealed record UpdateTagCommand(Guid Id, string Name, bool PinsToTop, Guid? Actor) : ICommand<TagDto>;
