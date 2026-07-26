using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.UpdateMyTaxProfile;

public sealed record UpdateMyTaxProfileCommand(
    Guid UserId,
    string TaxCode,
    string LegalName,
    string? Address,
    string? Email) : ICommand<UpdateMyTaxProfileResponse>;
