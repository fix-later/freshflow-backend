using System.Security.Claims;
using FreshFlow.API.Extensions;
using FreshFlow.Invoicing.Application.Queries.GetInvoiceById;
using FreshFlow.Invoicing.Application.Queries.GetInvoices;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize(Roles = "admin,operations_manager,restaurant")]
public sealed class InvoicesController(ISender sender) : ControllerBase
{
    /// <summary>Lists VAT invoices. Admin/ops see all; a restaurant sees only its own.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? restaurantId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetInvoicesQuery(ResolveUserId(), IsPrivileged(), restaurantId, status, page, pageSize);
        var result = await sender.Send(query, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    /// <summary>Returns a single invoice with its lines and tax-authority lookup URL.</summary>
    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> GetAsync(Guid invoiceId, CancellationToken ct)
    {
        var query = new GetInvoiceByIdQuery(ResolveUserId(), IsPrivileged(), invoiceId);
        var result = await sender.Send(query, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Ok(result.Value))
            : result.Error.ToActionResult();
    }

    private bool IsPrivileged() => User.IsInRole("admin") || User.IsInRole("operations_manager");

    private Guid ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim is missing or malformed.");
    }
}
