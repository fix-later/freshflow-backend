namespace FreshFlow.Logistics.Application.Dtos;

public sealed record RouteSuggestionsDto(
    DateOnly ServiceDate,
    IReadOnlyList<SuggestionItemDto> Hubs,
    IReadOnlyList<SuggestionItemDto> Restaurants);
