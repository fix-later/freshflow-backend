namespace FreshFlow.Auth.Application.Commands.UpdateMyTaxProfile;

public sealed record UpdateMyTaxProfileResponse(
    Guid Id,
    string? TaxCode,
    string? LegalName,
    string? Address,
    string? Email,
    DateTime UpdatedAt);
