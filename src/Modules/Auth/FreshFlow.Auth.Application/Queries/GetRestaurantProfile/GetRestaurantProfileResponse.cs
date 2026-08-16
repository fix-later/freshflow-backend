namespace FreshFlow.Auth.Application.Queries.GetRestaurantProfile;

public sealed record GetRestaurantProfileResponse(
    Guid RestaurantId,
    string Name,
    string Status,
    string? Address,
    string? ContactPerson,
    TimeOnly? PickupStart,
    TimeOnly? PickupEnd,
    DateTime UpdatedAt,
    string? BusinessLicenseUrl = null,
    string? TaxCode = null,
    string? InvoiceLegalName = null,
    string? InvoiceAddress = null,
    string? InvoiceEmail = null);
