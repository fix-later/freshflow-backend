using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Application.Common;
using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Invoicing.Application.Services;

/// <summary>
/// Turns a delivered order into an issued VAT invoice and drives retries. Shared by the
/// DeliveryCompleted event handler (initial issuance) and the retry background job.
/// </summary>
public sealed class InvoiceIssuanceService(
    IInvoiceRepository invoices,
    IOrderInvoiceReader orderReader,
    IRestaurantReader restaurantReader,
    IEInvoiceProvider provider,
    ILogger<InvoiceIssuanceService> logger) : IInvoiceIssuanceService
{
    private const string ProviderExceptionReason = "PROVIDER_EXCEPTION";

    public async Task IssueForDeliveredOrderAsync(Guid orderId, CancellationToken ct)
    {
        if (await invoices.ExistsForOrderAsync(orderId, ct))
            return; // idempotent — already invoiced or in-flight

        var order = await orderReader.GetByOrderIdAsync(orderId, ct);
        if (order is null || order.Lines.Count == 0)
        {
            logger.LogWarning("No billable order data for OrderId={OrderId}; skipping invoice.", orderId);
            return;
        }

        if (order.Lines.Any(l =>
                string.IsNullOrWhiteSpace(l.ProductName) ||
                string.IsNullOrWhiteSpace(l.Unit) ||
                l.Quantity < 0m ||
                l.UnitPrice < 0m))
        {
            logger.LogWarning("Invalid billable order data for OrderId={OrderId}; skipping invoice.", orderId);
            return;
        }

        var profile = await restaurantReader.GetTaxProfileAsync(order.RestaurantId, ct);

        var lines = order.Lines.Select(l =>
        {
            var code = VatRateResolver.Normalize(l.VatRateCode);
            return new InvoiceLine(
                l.ProductName, l.Unit!, l.Quantity, l.UnitPrice, code, VatRateResolver.ToPercent(code));
        }).ToList();

        if (order.DeliveryFee > 0m)
            lines.Add(new InvoiceLine(
                InvoiceLineNames.DeliveryFee, InvoiceLineNames.DeliveryFeeUnit,
                1m, order.DeliveryFee, VatRateResolver.CodeKct, 0m));

        var invoice = new Invoice(
            order.OrderId,
            order.RestaurantId,
            profile?.TaxCode ?? string.Empty,
            profile?.LegalName ?? string.Empty,
            profile?.Address,
            profile?.Email,
            lines);

        await invoices.AddAsync(invoice, ct);
        await invoices.SaveChangesAsync(ct);

        // First attempt never gives up immediately; a failure lands in PendingIssuance for the retry job.
        await AttemptIssueAsync(invoice, maxAttempts: int.MaxValue, ct);
        await invoices.SaveChangesAsync(ct);
    }

    public async Task<int> ReconcileMissingAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize <= 0)
            throw new ArgumentException("batchSize must be greater than zero.", nameof(batchSize));

        var orderIds = await orderReader.GetUninvoicedDeliveredOrderIdsAsync(batchSize, ct);
        var processed = 0;
        foreach (var orderId in orderIds)
        {
            try
            {
                await IssueForDeliveredOrderAsync(orderId, ct);
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to reconcile missing invoice for OrderId={OrderId}.",
                    orderId);
            }
        }

        return processed;
    }

    public async Task<int> RetryDueAsync(int maxAttempts, TimeSpan backoff, int batchSize, CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.Subtract(backoff);
        var due = await invoices.GetRetryablePageAsync(maxAttempts, threshold, batchSize, ct);

        var processed = 0;
        foreach (var invoice in due)
        {
            try
            {
                var profile = await restaurantReader.GetTaxProfileAsync(invoice.RestaurantId, ct);
                var legalName = profile?.LegalName;
                if (InvoiceBuyerValidator.GetErrorCode(profile?.TaxCode, legalName, profile?.Address) is null)
                    invoice.UpdateBuyer(profile!.TaxCode!, legalName!, profile.Address, profile.Email);

                await AttemptIssueAsync(invoice, maxAttempts, ct);
                await invoices.SaveChangesAsync(ct);
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invoice retry failed for InvoiceId={InvoiceId}.", invoice.Id);
            }
        }

        return processed;
    }

    private async Task AttemptIssueAsync(Invoice invoice, int maxAttempts, CancellationToken ct)
    {
        var buyerError = InvoiceBuyerValidator.GetErrorCode(
            invoice.BuyerTaxCode, invoice.BuyerLegalName, invoice.BuyerAddress);
        if (buyerError is not null)
        {
            invoice.MarkAwaitingBuyerInfo(buyerError);
            return;
        }

        if (invoice.Lines.Any(line => string.IsNullOrWhiteSpace(line.Unit)))
        {
            invoice.MarkFailed("INVOICE_LINE_UNIT_REQUIRED");
            return;
        }

        try
        {
            var request = new InvoiceIssueRequest(
                invoice.Id,
                invoice.OrderId,
                invoice.BuyerTaxCode,
                invoice.BuyerLegalName,
                invoice.BuyerAddress,
                invoice.BuyerEmail,
                invoice.SubTotal,
                invoice.VatAmount,
                invoice.Total,
                invoice.Lines
                    .Select(l => new InvoiceIssueLine(
                        l.ProductName,
                        l.Unit!,
                        l.Quantity,
                        l.UnitPrice,
                        l.VatRateCode,
                        l.VatRatePercent,
                        l.LineSubtotal,
                        l.LineVatAmount,
                        l.LineTotal))
                    .ToList());

            var result = await provider.IssueAsync(request, ct);
            if (result.IsSuccess)
            {
                var issued = result.Value;
                invoice.MarkIssued(
                    issued.Serial, issued.Number, issued.TaxAuthorityCode,
                    issued.LookupUrl, issued.XmlRef, issued.PdfRef, provider.Name, issued.IssuedAt);
            }
            else
            {
                logger.LogWarning(
                    "E-invoice provider failed for InvoiceId={InvoiceId}: Code={ErrorCode}, Message={ErrorMessage}.",
                    invoice.Id, result.Error.Code, result.Error.Message);
                FailOrGiveUp(invoice, result.Error.Code, maxAttempts);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "E-invoice provider threw for InvoiceId={InvoiceId}.", invoice.Id);
            FailOrGiveUp(invoice, ProviderExceptionReason, maxAttempts);
        }
    }

    private static void FailOrGiveUp(Invoice invoice, string reason, int maxAttempts)
    {
        if (invoice.RetryCount + 1 >= maxAttempts)
            invoice.MarkFailed(reason);
        else
            invoice.MarkIssuanceFailed(reason);
    }
}
