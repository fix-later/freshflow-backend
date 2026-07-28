using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.GetRouteSuggestions;

public sealed record GetRouteSuggestionsQuery(
    DateOnly ServiceDate,
    bool IncludeBatched = false) : IQuery<RouteSuggestionsDto>;
