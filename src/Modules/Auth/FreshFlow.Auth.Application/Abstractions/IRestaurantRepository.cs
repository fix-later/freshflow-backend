using FreshFlow.Auth.Domain.Enums;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IRestaurantRepository
{
    public Task<Guid> CreateAsync(
        Guid userId,
        string restaurantName,
        string? taxCode,
        string? invoiceLegalName,
        string? invoiceAddress,
        CancellationToken ct);
    public Task<RestaurantDto?> FindByIdAsync(Guid restaurantId, CancellationToken ct);
    public Task<RestaurantDto?> FindByUserIdAsync(Guid userId, CancellationToken ct);
    public Task<bool> ApproveAsync(Guid restaurantId, CancellationToken ct);
    public Task<bool> SuspendAsync(Guid restaurantId, CancellationToken ct);
    public Task<RestaurantDto?> UpdateProfileAsync(
        Guid restaurantId,
        string name,
        string? address,
        string? contactPerson,
        TimeOnly? pickupStart,
        TimeOnly? pickupEnd,
        string? businessLicenseUrl,
        CancellationToken ct);
    public Task<RestaurantDto?> UpdateTaxProfileAsync(
        Guid restaurantId,
        string taxCode,
        string legalName,
        string? address,
        string? email,
        CancellationToken ct);
}

public sealed record RestaurantDto(
    Guid Id,
    string Name,
    RestaurantStatus Status,
    DateTime UpdatedAt,
    Guid UserId,
    string? Address = null,
    string? ContactPerson = null,
    TimeOnly? PickupStart = null,
    TimeOnly? PickupEnd = null,
    string? BusinessLicenseUrl = null,
    string? TaxCode = null,
    string? InvoiceLegalName = null,
    string? InvoiceAddress = null,
    string? InvoiceEmail = null)
{
    /// <summary>
    /// Backward-compatible convenience property.
    /// True when <see cref="Status"/> is <see cref="RestaurantStatus.Active"/>.
    /// </summary>
    public bool IsApproved => Status == RestaurantStatus.Active;
}
