using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.CreateTag;

/// <summary>POST /api/v1/tags — admin only.</summary>
public sealed record CreateTagCommand(string Name, bool PinsToTop, Guid? Actor) : ICommand<TagDto>;
