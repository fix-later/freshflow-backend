using FluentAssertions;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Application.Services;
using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Invoicing.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class InvoiceIssuanceServiceTests
{
    private readonly IInvoiceRepository _repo = Substitute.For<IInvoiceRepository>();
    private readonly IOrderInvoiceReader _orderReader = Substitute.For<IOrderInvoiceReader>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IEInvoiceProvider _provider = Substitute.For<IEInvoiceProvider>();
    private readonly InvoiceIssuanceService _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public InvoiceIssuanceServiceTests()
    {
        _provider.Name.Returns("stub");
        _sut = new InvoiceIssuanceService(
            _repo, _orderReader, _restaurantReader, _provider,
            Substitute.For<ILogger<InvoiceIssuanceService>>());
    }

    [Fact]
    public async Task IssueForDeliveredOrder_AlreadyInvoiced_SkipsAsync()
    {
        _repo.ExistsForOrderAsync(OrderId, Arg.Any<CancellationToken>()).Returns(true);

        await _sut.IssueForDeliveredOrderAsync(OrderId, default);

        await _repo.DidNotReceive().AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueForDeliveredOrder_Success_MarksIssuedAsync()
    {
        _repo.ExistsForOrderAsync(OrderId, Arg.Any<CancellationToken>()).Returns(false);
        ArrangeBillableOrder(deliveryFee: 10_000m);
        _restaurantReader.GetTaxProfileAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantTaxProfile(RestaurantId, "Nhà hàng A", "0312345678", "Cty A", "Addr", "a@x.vn"));
        InvoiceIssueRequest? request = null;
        _provider.IssueAsync(Arg.Do<InvoiceIssueRequest>(r => request = r), Arg.Any<CancellationToken>())
            .Returns(Result<IssuedInvoice>.Success(Issued()));

        var captured = CaptureAddedInvoice();

        await _sut.IssueForDeliveredOrderAsync(OrderId, default);

        captured().Should().NotBeNull();
        captured()!.Status.Should().Be(InvoiceStatus.Issued);
        captured()!.TaxAuthorityCode.Should().Be("MCQT-1");
        captured()!.BuyerTaxCode.Should().Be("0312345678");
        captured()!.SubTotal.Should().Be(60_000m);
        captured()!.VatAmount.Should().Be(2500m);
        captured()!.Total.Should().Be(62_500m);
        request.Should().NotBeNull();
        request!.Lines.Should().HaveCount(2);
        request.Lines[0].Should().Be(new InvoiceIssueLine(
            "Cà chua", "thùng", 10m, 5000m, "5", 5m, 50000m, 2500m, 52500m));
        request.Lines[1].Should().Be(new InvoiceIssueLine(
            "Phí giao hàng", "lần", 1m, 10_000m, "KCT", 0m, 10_000m, 0m, 10_000m));
    }

    [Fact]
    public async Task IssueForDeliveredOrder_ProviderFails_LeavesPendingAsync()
    {
        _repo.ExistsForOrderAsync(OrderId, Arg.Any<CancellationToken>()).Returns(false);
        ArrangeBillableOrder();
        _restaurantReader.GetTaxProfileAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantTaxProfile(RestaurantId, "Nhà hàng A", "0312345678", "Cty A", "Addr", "a@x.vn"));
        _provider.IssueAsync(Arg.Any<InvoiceIssueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<IssuedInvoice>.Failure(Error.Conflict("PROVIDER_DOWN", "down")));

        var captured = CaptureAddedInvoice();

        await _sut.IssueForDeliveredOrderAsync(OrderId, default);

        captured()!.Status.Should().Be(InvoiceStatus.PendingIssuance);
        captured()!.RetryCount.Should().Be(1);
        captured()!.ErrorReason.Should().Be("PROVIDER_DOWN");
    }

    [Fact]
    public async Task IssueForDeliveredOrder_IncompleteBuyer_StaysPendingWithoutCallingProviderAsync()
    {
        _repo.ExistsForOrderAsync(OrderId, Arg.Any<CancellationToken>()).Returns(false);
        ArrangeBillableOrder();
        _restaurantReader.GetTaxProfileAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantTaxProfile(RestaurantId, "Nhà hàng A", " ", "Cty A", "Addr", "a@x.vn"));

        var captured = CaptureAddedInvoice();

        await _sut.IssueForDeliveredOrderAsync(OrderId, default);

        captured()!.Status.Should().Be(InvoiceStatus.PendingIssuance);
        captured()!.RetryCount.Should().Be(0);
        captured()!.ErrorReason.Should().Be("BUYER_TAX_CODE_REQUIRED");
        await _provider.DidNotReceive().IssueAsync(
            Arg.Any<InvoiceIssueRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryDue_CompleteBuyerProfile_UpdatesBuyerAndIssuesAsync()
    {
        var invoice = new Invoice(
            OrderId, RestaurantId, string.Empty, string.Empty, null, null,
            [new InvoiceLine("A", "kg", 1m, 1000m, "KCT", 0m)]);
        invoice.MarkAwaitingBuyerInfo("MISSING_BUYER_TAX_PROFILE");

        _repo.GetRetryablePageAsync(5, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { invoice });
        _restaurantReader.GetTaxProfileAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantTaxProfile(
                RestaurantId, "Nhà hàng A", "0312345678", "Cty A", "Addr", "a@x.vn"));
        _provider.IssueAsync(Arg.Any<InvoiceIssueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<IssuedInvoice>.Success(Issued()));

        var processed = await _sut.RetryDueAsync(5, TimeSpan.Zero, 10, default);

        processed.Should().Be(1);
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.BuyerTaxCode.Should().Be("0312345678");
        invoice.BuyerLegalName.Should().Be("Cty A");
        invoice.BuyerAddress.Should().Be("Addr");
        invoice.BuyerEmail.Should().Be("a@x.vn");
    }

    [Fact]
    public async Task RetryDue_ExhaustsAttempts_MarksFailedAsync()
    {
        var invoice = new Invoice(
            OrderId, RestaurantId, "0312345678", "Cty A", "Addr", null,
            [new InvoiceLine("A", "kg", 1m, 1000m, "KCT", 0m)]);
        for (var i = 0; i < 4; i++)
            invoice.MarkIssuanceFailed("prev"); // RetryCount → 4 (cap is 5)

        _repo.GetRetryablePageAsync(5, Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { invoice });
        _provider.IssueAsync(Arg.Any<InvoiceIssueRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<IssuedInvoice>.Failure(Error.Conflict("PROVIDER_DOWN", "down")));

        var processed = await _sut.RetryDueAsync(maxAttempts: 5, backoff: TimeSpan.Zero, batchSize: 10, default);

        processed.Should().Be(1);
        invoice.Status.Should().Be(InvoiceStatus.Failed);
    }

    private void ArrangeBillableOrder(decimal deliveryFee = 0m) =>
        _orderReader.GetByOrderIdAsync(OrderId, Arg.Any<CancellationToken>())
            .Returns(new OrderInvoiceSnapshot(
                OrderId, RestaurantId, deliveryFee,
                [new OrderInvoiceLineSnapshot("Cà chua", "thùng", 10m, 5000m, "5")]));

    private Func<Invoice?> CaptureAddedInvoice()
    {
        Invoice? captured = null;
        _repo.When(r => r.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>()))
            .Do(ci => captured = ci.Arg<Invoice>());
        return () => captured;
    }

    private static IssuedInvoice Issued() =>
        new("K24TFF", "0001", "MCQT-1", "http://x", null, null, DateTime.UtcNow);
}
