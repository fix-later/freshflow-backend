using FluentAssertions;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Application.Queries.GetInvoiceById;
using FreshFlow.Invoicing.Application.Queries.GetInvoices;
using FreshFlow.Invoicing.Application.Queries.GetInvoiceSummary;
using FreshFlow.Invoicing.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Invoicing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class InvoiceQueryHandlerTests
{
    private readonly IInvoiceRepository _repo = Substitute.For<IInvoiceRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();

    // ---- GetInvoices ----

    [Fact]
    public async Task GetInvoices_NonAdminWithNoRestaurant_ForbiddenAsync()
    {
        _restaurantReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns((Guid?)null);
        var handler = new GetInvoicesQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new GetInvoicesQuery(UserId, false, null, null, 1, 20), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task GetInvoices_NonAdminRequestingOtherRestaurant_NotFoundAsync()
    {
        _restaurantReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(RestaurantId);
        var handler = new GetInvoicesQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(
            new GetInvoicesQuery(UserId, false, OtherRestaurantId, null, 1, 20), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task GetInvoices_NonAdmin_ScopesToOwnRestaurantAsync()
    {
        _restaurantReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(RestaurantId);
        _repo.ListAsync(RestaurantId, null, 0, 20, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Invoice>(), 0));
        var handler = new GetInvoicesQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new GetInvoicesQuery(UserId, false, null, null, 1, 20), default);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received().ListAsync(RestaurantId, null, 0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInvoices_InvalidStatus_ValidationErrorAsync()
    {
        var handler = new GetInvoicesQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new GetInvoicesQuery(UserId, true, null, "bogus", 1, 20), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetInvoiceSummary_NonAdminScopesAndReturnsIssuedTotalsAsync()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(7));
        var to = from.AddMonths(1);
        _restaurantReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(RestaurantId);
        _repo.SummarizeIssuedAsync(
                RestaurantId, from.UtcDateTime, to.UtcDateTime, Arg.Any<CancellationToken>())
            .Returns(new InvoiceTotals(2, 100m, 8m, 108m));
        var handler = new GetInvoiceSummaryQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(
            new GetInvoiceSummaryQuery(UserId, false, null, from, to), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.InvoiceCount.Should().Be(2);
        result.Value.Total.Should().Be(108m);
        result.Value.Notice.Should().Contain("không phải hóa đơn VAT");
    }

    [Fact]
    public async Task GetInvoiceSummary_InvalidPeriod_ReturnsValidationErrorAsync()
    {
        var handler = new GetInvoiceSummaryQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(
            new GetInvoiceSummaryQuery(UserId, true, null, null, null), default);

        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    // ---- GetInvoiceById ----

    [Fact]
    public async Task GetInvoiceById_NotFound_ReturnsNotFoundAsync()
    {
        _repo.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Invoice?)null);
        var handler = new GetInvoiceByIdQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new GetInvoiceByIdQuery(UserId, true, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVOICE_NOT_FOUND");
    }

    [Fact]
    public async Task GetInvoiceById_NonAdminOtherRestaurant_NotFoundIdorAsync()
    {
        var invoice = InvoiceFor(OtherRestaurantId);
        _repo.FindByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _restaurantReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(RestaurantId);
        var handler = new GetInvoiceByIdQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new GetInvoiceByIdQuery(UserId, false, invoice.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVOICE_NOT_FOUND"); // IDOR masked as 404
    }

    [Fact]
    public async Task GetInvoiceById_OwnInvoice_ReturnsItAsync()
    {
        var invoice = InvoiceFor(RestaurantId);
        _repo.FindByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        _restaurantReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(RestaurantId);
        var handler = new GetInvoiceByIdQueryHandler(_repo, _restaurantReader);

        var result = await handler.Handle(new GetInvoiceByIdQuery(UserId, false, invoice.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(RestaurantId);
    }

    private static Invoice InvoiceFor(Guid restaurantId) =>
        new(Guid.NewGuid(), restaurantId, "0312345678", "Cty A", "123 Nguyễn Huệ", null,
            [new InvoiceLine("A", "kg", 1m, 1000m, "KCT", 0m)]);
}
