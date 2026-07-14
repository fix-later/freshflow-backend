using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementExceptionReportedDomainEvent(
    Guid BatchId,
    Guid ExceptionId,
    Guid MarketProductId,
    ProcurementExceptionType Type) : IDomainEvent;
