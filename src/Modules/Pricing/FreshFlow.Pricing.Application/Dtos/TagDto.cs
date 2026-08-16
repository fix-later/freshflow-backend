namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>Tag catalog entry, returned by the tag CRUD endpoints.</summary>
public sealed record TagDto(Guid Id, string Name, bool PinsToTop);
