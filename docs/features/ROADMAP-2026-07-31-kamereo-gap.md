# Kamereo GAP roadmap — 2026-07-31

This file is the source of truth for the sequential Kamereo GAP rollout. Each GAP moves through
`Planned -> In progress -> Ready for review -> Done`; only a user-confirmed commit unlocks the next GAP.

| GAP | Scope | Status | Acceptance criteria |
| --- | --- | --- | --- |
| GAP-01 | PostgreSQL atomic stock reservation | Done | Confirmation reserves available stock and charges credit atomically; confirmed cancellation releases both once; procurement handover consumes reservations once; price board, search, and favorites expose `CurrentQuantity - ReservedQuantity`; concurrency and rollback paths are covered by PostgreSQL tests. |
| GAP-02 | Delivery-address snapshot | Planned | An order keeps the delivery address captured at placement even if the restaurant address later changes. |
| GAP-03 | MOQ, VAT, and distance delivery fee | Planned | Confirmation validates MOQ and returns deterministic VAT and distance-based delivery fee amounts. |
| GAP-04 | Actual purchase price and pro-rata shortage allocation | Planned | Purchased quantities/prices are recorded and shortages are allocated deterministically across affected orders. |
| GAP-05 | E-invoice readiness | Planned | Invoice data required for compliant e-invoice issuance is captured, validated, and exportable. |
| GAP-06 | Organizations, branches, and approval | Planned | Organization/branch boundaries and approval rules are enforced for ordering and administration. |
| GAP-07 | Spend analytics | Planned | Authorized users can query consistent spend aggregates over supported periods and dimensions. |
| GAP-08 | Claims and refunds | Planned | Claims have an auditable lifecycle and approved refunds update credit exactly once. |
| GAP-09 | Delivery slips | Planned | Delivery slips are generated from immutable fulfillment data and remain retrievable. |
| GAP-10 | QC traceability | Planned | QC results can be traced from procurement/lot through fulfillment and delivery. |

## GAP-01 — PostgreSQL atomic stock reservation

### Scope and API contract

- Reuse `market_products.ReservedQuantity`; no Redis, new table, or migration.
- Confirm an order inside a PostgreSQL `Serializable` transaction.
- Aggregate quantities by `MarketProductId`, process IDs in ascending order, and reserve with a
  parameterized conditional update only when `CurrentQuantity - ReservedQuantity >= requested`.
- Return `INSUFFICIENT_STOCK` when a conditional reservation cannot be made. Return a retryable
  conflict for PostgreSQL serialization failures. The confirm request/response stays unchanged.
- Cancelling a `Confirmed` order, directly or through a procurement-session cancellation, releases
  its reservation and refunds its credit in one transaction.
- Procurement handover consumes the reservation before moving an order out of `Batched`; repeats are no-ops.
- `AvailableQuantity` means `CurrentQuantity - ReservedQuantity` in price board, search, and favorites.

### Acceptance criteria

- [x] Two concurrent orders competing for the final quantity produce exactly one success.
- [x] Two concurrent confirmations of one order charge credit and reserve stock once.
- [x] One insufficient line in a multi-line order rolls back every reservation and credit/order change.
- [x] A failed credit charge leaves stock unreserved and the order unconfirmed.
- [x] Cancelling a confirmed order restores stock availability and credit exactly once.
- [x] A repeated handover/event does not consume inventory twice.
- [x] Price board, search, and favorites return the same `AvailableQuantity` formula.
- [x] Solution build, Orders/Pricing unit tests, and related PostgreSQL integration tests pass.

### Change record

- Files changed:
  - `src/FreshFlow.API/Extensions/ErrorExtensions.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/IOrderRepository.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/CancelOrder/CancelOrderCommandHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/ConfirmOrder/ConfirmOrderCommandHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/EventHandlers/ProcurementBatchCancelledIntegrationEventHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/EventHandlers/ProcurementBatchHandedOffIntegrationEventHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Repositories/OrderRepository.cs`
  - `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Cache/DbPriceBoardReader.cs`
  - `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Repositories/MarketProductRepository.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Commands/CancelOrderCommandHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Commands/ConfirmOrderCommandHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/EventHandlers/ProcurementBatchCancelledIntegrationEventHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/EventHandlers/ProcurementBatchHandedOffIntegrationEventHandlerTests.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Orders/AtomicStockReservationEndpointTests.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Orders/RestaurantFavoritesEndpointTests.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Pricing/MarketProductRepositorySearchTests.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Pricing/PriceBoardOverlayEndpointTests.cs`
- Migration: none.
- API contract: confirm request/response unchanged; `INSUFFICIENT_STOCK` is returned for unavailable
  stock; `SERIALIZATION_CONFLICT` is a retryable HTTP 409; `AvailableQuantity` now excludes reservations.
- Test results: solution build passed (0 errors, 21 existing warnings); Orders unit tests 541/541;
  Pricing unit tests 277/277; related PostgreSQL integration tests 19/19.

## Decision log

- 2026-07-31: Use the existing PostgreSQL column and request-scoped `AppDbContext`; do not add Redis,
  an inventory table, or a new migration.
- 2026-07-31: Keep every GAP as a separate review and commit. GAP-02 remains blocked until the user
  confirms GAP-01 has been committed.
- 2026-07-31: Handover derives quantities from the covered orders, so no handover contract expansion
  is needed in GAP-01; actual purchased quantity and shortage allocation remain in GAP-04.
- 2026-07-31: Pricing update tracking never writes `ReservedQuantity`, preventing a stale price or
  quantity update from overwriting reservations owned by Orders.
- 2026-07-31: Procurement-session cancellation uses the same serializable stock/credit transaction;
  repeated cancellation events are no-ops.
