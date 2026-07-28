namespace FreshFlow.Logistics.Application.Dtos;

public sealed record RouteSuggestionsDto(
    DateOnly ServiceDate,
    IReadOnlyList<SuggestionItemDto> Markets,
    IReadOnlyList<SuggestionItemDto> Restaurants);
