namespace FreshFlow.Logistics.Application.Dtos;

public sealed record EligibilityResultDto(bool IsEligible, IReadOnlyList<string> Reasons);
