using System.Security.Claims;
using System.Text;
using FreshFlow.API.Extensions;
using FreshFlow.Invoicing.Application.Queries.ExportInvoice;
using FreshFlow.Invoicing.Application.Queries.GetInvoiceById;
using FreshFlow.Invoicing.Application.Queries.GetInvoices;
using FreshFlow.Invoicing.Application.Queries.GetInvoiceSummary;
using FreshFlow.Invoicing.Infrastructure.Documents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize(Roles = "admin,operations_manager,restaurant")]
public sealed class InvoicesController(ISender sender, InvoicePdfRenderer pdfRenderer) : ControllerBase
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

    /// <summary>Aggregates issued invoices for reconciliation; this is not a VAT invoice.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> SummaryAsync(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? restaurantId,
        CancellationToken ct)
    {
        var query = new GetInvoiceSummaryQuery(
            ResolveUserId(), IsPrivileged(), restaurantId, from, to);
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

    /// <summary>Exports one issued invoice as its persisted structured XML document.</summary>
    [HttpGet("{invoiceId:guid}/export")]
    public async Task<IActionResult> ExportAsync(Guid invoiceId, CancellationToken ct)
    {
        var query = new ExportInvoiceQuery(ResolveUserId(), IsPrivileged(), invoiceId);
        var result = await sender.Send(query, ct);
        return result.IsSuccess
            ? File(Encoding.UTF8.GetBytes(result.Value.Xml), "application/xml", result.Value.FileName)
            : result.Error.ToActionResult();
    }

    /// <summary>Downloads a sandbox invoice as a clearly marked, non-legal PDF draft.</summary>
    [HttpGet("{invoiceId:guid}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DownloadPdfAsync(Guid invoiceId, CancellationToken ct)
    {
        var result = await sender.Send(
            new ExportInvoiceQuery(ResolveUserId(), IsPrivileged(), invoiceId), ct);
        if (result.IsFailure)
            return result.Error.ToActionResult();

        if (!result.Value.Invoice.IsSandbox)
            return UnprocessableEntity(ApiResponse.Err(
                "INVOICE_PDF_PROVIDER_REQUIRED",
                "A provider-issued invoice must use the PDF supplied by that provider."));

        return File(
            pdfRenderer.Render(result.Value.Invoice),
            "application/pdf",
            Path.ChangeExtension(result.Value.FileName, ".pdf"));
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
