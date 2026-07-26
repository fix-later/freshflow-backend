using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Auth.Application.Commands.CreateLicenseUploadSignature;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Add;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Delete;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Update;
using FreshFlow.Auth.Application.Commands.UpdateMyTaxProfile;
using FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;
using FreshFlow.Auth.Application.Queries.GetDeliveryAddresses;
using FreshFlow.Auth.Application.Queries.GetRestaurantApprovalStatus;
using FreshFlow.Auth.Application.Queries.GetRestaurantProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/restaurants")]
[Authorize(Roles = "restaurant")]
public sealed class RestaurantProfileController(ISender sender) : ControllerBase
{
    // ── Approval Status ──────────────────────────────────────────────────────

    /// <summary>GET /api/v1/restaurants/me/approval-status — returns the restaurant's current approval status.</summary>
    [HttpGet("me/approval-status")]
    public async Task<IActionResult> GetApprovalStatusAsync(CancellationToken ct)
    {
        var result = await sender.Send(
            new GetRestaurantApprovalStatusQuery(ResolveUserId()), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/restaurants/me/tax-profile — updates the authenticated restaurant's invoice tax profile.</summary>
    [HttpPut("me/tax-profile")]
    public async Task<IActionResult> UpdateTaxProfileAsync(
        [FromBody] UpdateTaxProfileRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateMyTaxProfileCommand(
                ResolveUserId(),
                body.TaxCode,
                body.LegalName,
                body.Address,
                body.Email),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Profile ──────────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/restaurants/me/profile — returns the authenticated restaurant's profile.</summary>
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetProfileAsync(CancellationToken ct)
    {
        var result = await sender.Send(
            new GetRestaurantProfileQuery(ResolveUserId()), ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/restaurants/me/profile — updates the authenticated restaurant's profile.</summary>
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateProfileAsync(
        [FromBody] UpdateRestaurantProfileRequest body, CancellationToken ct)
    {
        var userId = ResolveUserId();

        var result = await sender.Send(
            new UpdateRestaurantProfileCommand(
                userId,
                body.Name,
                body.Address,
                body.ContactPerson,
                body.PickupStart,
                body.PickupEnd,
                body.BusinessLicenseUrl),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/restaurants/me/business-license/upload-signature — signs a Cloudinary
    /// business-license upload.
    /// </summary>
    [HttpPost("me/business-license/upload-signature")]
    public async Task<IActionResult> CreateLicenseUploadSignatureAsync(CancellationToken ct)
    {
        var result = await sender.Send(new CreateLicenseUploadSignatureCommand(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    // ── Delivery Addresses ───────────────────────────────────────────────────

    /// <summary>GET /api/v1/restaurants/me/delivery-addresses — lists all active delivery addresses.</summary>
    [HttpGet("me/delivery-addresses")]
    public async Task<IActionResult> GetDeliveryAddressesAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetDeliveryAddressesQuery(ResolveUserId()), ct);
        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>POST /api/v1/restaurants/me/delivery-addresses — adds a delivery address.</summary>
    [HttpPost("me/delivery-addresses")]
    public async Task<IActionResult> AddDeliveryAddressAsync(
        [FromBody] DeliveryAddressRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new AddDeliveryAddressCommand(
                ResolveUserId(),
                body.RecipientName,
                body.Phone,
                body.AddressLine,
                body.Latitude,
                body.Longitude,
                body.IsDefault),
            ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDeliveryAddressesAsync), null, ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>PUT /api/v1/restaurants/me/delivery-addresses/{id} — updates a delivery address.</summary>
    [HttpPut("me/delivery-addresses/{id:guid}")]
    public async Task<IActionResult> UpdateDeliveryAddressAsync(
        Guid id, [FromBody] DeliveryAddressRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateDeliveryAddressCommand(
                ResolveUserId(),
                id,
                body.RecipientName,
                body.Phone,
                body.AddressLine,
                body.Latitude,
                body.Longitude,
                body.IsDefault),
            ct);

        return result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult();
    }

    /// <summary>DELETE /api/v1/restaurants/me/delivery-addresses/{id} — soft-deletes a delivery address.</summary>
    [HttpDelete("me/delivery-addresses/{id:guid}")]
    public async Task<IActionResult> DeleteDeliveryAddressAsync(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(
            new DeleteDeliveryAddressCommand(ResolveUserId(), id), ct);

        return result.IsSuccess ? NoContent() : result.Error.ToActionResult();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim is missing or malformed.");
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record UpdateRestaurantProfileRequest(
    string Name,
    string? Address,
    string? ContactPerson,
    TimeOnly? PickupStart,
    TimeOnly? PickupEnd,
    string? BusinessLicenseUrl = null);

public sealed record UpdateTaxProfileRequest(
    string TaxCode,
    string LegalName,
    string? Address,
    string? Email);

public sealed record DeliveryAddressRequest(
    string AddressLine,
    string? RecipientName,
    string? Phone,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault);
