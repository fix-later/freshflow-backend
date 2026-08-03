# Kamereo GAP roadmap — 2026-07-31

This file is the source of truth for the sequential Kamereo GAP rollout. Each GAP moves through
`Planned -> In progress -> Ready for review -> Done`; only a user-confirmed commit unlocks the next GAP.

| GAP | Scope | Status | Acceptance criteria |
| --- | --- | --- | --- |
| GAP-01 | PostgreSQL atomic stock reservation | Done | Confirmation reserves available stock and charges credit atomically; confirmed cancellation releases both once; procurement handover consumes reservations once; price board, search, and favorites expose `CurrentQuantity - ReservedQuantity`; concurrency and rollback paths are covered by PostgreSQL tests. |
| GAP-02 | Delivery-address snapshot | Done | An order keeps the delivery address captured at placement even if the restaurant address later changes. |
| GAP-03 | MOQ, VAT, and distance delivery fee | Done | Confirmation validates MOQ and returns deterministic VAT and distance-based delivery fee amounts. |
| GAP-04 | Actual purchase price and pro-rata shortage allocation | Done | Purchased quantities/prices are recorded and shortages are allocated deterministically across affected orders. |
| GAP-05 | E-invoice readiness | Planned ⚠️ reconcile | Invoice data required for compliant e-invoice issuance is captured, validated, and exportable. |
| GAP-06 | Organizations, branches, and approval | Planned ⛔ blocked | Organization/branch boundaries and approval rules are enforced for ordering and administration. |
| GAP-07 | Spend analytics | Planned ⚠️ reconcile | Authorized users can query consistent spend aggregates over supported periods and dimensions. |
| GAP-08 | Claims and refunds | Planned | Claims have an auditable lifecycle and approved refunds update credit exactly once. |
| GAP-09 | Delivery slips | Planned | Delivery slips are generated from immutable fulfillment data and remain retrievable. |
| GAP-10 | QC traceability | Planned | QC results can be traced from procurement/lot through fulfillment and delivery. |

## Reconciliation review — 2026-08-04

Review of GAP-01..04 (all Done, committed, merged tree builds 0 errors, Orders 553/553, Procurement
124/124) plus a scope check of the Planned GAPs against modules that already exist. GAP-01..04
implementation is sound; the notes below only affect the Planned GAPs and their ordering.

- **GAP-05 (E-invoice) — extend, do not greenfield.** An `Invoicing` module already exists
  (`src/Modules/Invoicing`, per-delivery issuance, VAT live from `products.VatRate`, cross-module
  seams proven on Postgres). GAP-03 already snapshots per-line VAT code/rate at confirmation. GAP-05
  must build on the existing Invoicing module (add compliant NCC export/validation), not rebuild
  issuance. Avoid a second parallel invoicing path.
- **GAP-07 (Spend analytics) — extend `Analytics`; clarify the dimension.** An `Analytics` module
  already exists (`src/Modules/Analytics`, read-only, 0 tables). GAP-07 must extend it. Decide up
  front whether spend aggregates are per-**restaurant** (no dependency, can ship early) or per-**org
  /branch** (depends on GAP-06). If restaurant-level suffices, do not couple GAP-07 to GAP-06.
- **GAP-06 (Organizations/branches/approval) — blocked pending a product decision.** This
  contradicts DEC-003 ("no multi-user restaurant accounts", removed on purpose). It is also the
  largest, cross-cutting change (Auth + Orders + Admin). Do not move it to In progress without an
  explicit product decision re-opening DEC-003. Given its size/risk and lower MVP urgency, prefer
  resequencing it after GAP-08.
- **Suggested resequence:** GAP-05 (extend Invoicing) → GAP-08 (claims/refund, self-contained, high
  value) → GAP-07 (analytics, restaurant-level) → GAP-09 (slips) → GAP-06 (orgs, only after the
  DEC-003 decision) → GAP-10 (QC traceability). GAP-08 refunds stay a credit adjustment (no gateway),
  consistent with the existing credit model.
- **GAP-04 nit check (no change needed):** allocation quantities are `int` end-to-end
  (`ProcurementPurchaseActual.ActualQuantity` and `OrderItem.Quantity`), so the largest-remainder
  split reconciles exactly; `totalRequested == 0` is unreachable (`OrderItem.ValidateQuantity`
  rejects `<= 0`). Order-independence + reconciliation is already locked by
  `Handle_RoundingTie_UsesOrderIdThenItemIdAsync`. No defensive guard added (would be dead code).

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

## GAP-02 — Delivery-address snapshot

### Scope and API contract

- Confirmation selects a restaurant-owned active delivery address by `deliveryAddressId`.
- Snapshot recipient name, phone, address line, latitude, and longitude into the order in the same
  transaction as confirmation; later address edits or deletion must not change the order.
- Return the immutable snapshot from order-detail responses.

### Acceptance criteria

- [x] Confirmation rejects a missing, deleted, or other restaurant's delivery address.
- [x] A confirmed order retains the selected address values after the source address changes.
- [x] Snapshot creation rolls back when confirmation fails.
- [x] Solution build, Orders unit tests, and related PostgreSQL integration tests pass.

### Change record

- Files changed:
  - `src/FreshFlow.API/Assistant/AssistantOrchestrator.cs`
  - `src/FreshFlow.API/Assistant/Dtos/AssistantChatRequest.cs`
  - `src/FreshFlow.API/Assistant/Dtos/AssistantChatResponse.cs`
  - `src/FreshFlow.API/Assistant/Safety/ConfirmationGate.cs`
  - `src/FreshFlow.API/Assistant/Safety/ConfirmationGateResult.cs`
  - `src/FreshFlow.API/Assistant/Tools/AssistantTool.cs`
  - `src/FreshFlow.API/Assistant/Tools/ToolDefinitions.cs`
  - `src/FreshFlow.API/Controllers/AssistantController.cs`
  - `src/FreshFlow.API/Controllers/OrdersController.cs`
  - `src/FreshFlow.Infrastructure.Persistence/Migrations/20260801143556_AddOrderDeliveryAddressSnapshot.cs`
  - `src/FreshFlow.Infrastructure.Persistence/Migrations/20260801143556_AddOrderDeliveryAddressSnapshot.Designer.cs`
  - `src/FreshFlow.Infrastructure.Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/IRestaurantReader.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/ConfirmOrder/ConfirmOrderCommand.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/ConfirmOrder/ConfirmOrderCommandHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/ConfirmOrder/ConfirmOrderCommandValidator.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OrderDto.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OrderDtoMapper.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/CrossModule/RestaurantReader.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/CrossModule/RestaurantRow.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/CrossModule/RestaurantRowConfiguration.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Orders/AtomicStockReservationEndpointTests.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Assistant/AssistantChatEndpointTests.cs`
  - `tests/Unit/FreshFlow.Assistant.UnitTests/Orchestration/AssistantOrchestratorTests.cs`
  - `tests/Unit/FreshFlow.Assistant.UnitTests/Safety/ConfirmationGateTests.cs`
  - `tests/Unit/FreshFlow.Assistant.UnitTests/Tools/ToolDefinitionsTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Commands/ConfirmOrderCommandHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Commands/ConfirmOrderCommandValidatorTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Domain/OrderTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Persistence/PersistenceConfigurationTests.cs`
- Migration: `20260801143556_AddOrderDeliveryAddressSnapshot` adds six nullable snapshot columns to
  `orders`; no foreign key is retained to the mutable source address.
- API contract: `POST /api/orders/{id}/confirm` requires `deliveryAddressId`; Assistant chat accepts
  a client-selected `deliveryAddressId`, binds it in the confirmation gate, and never exposes it as
  an LLM-controlled tool argument. Pending confirmation echoes the selected address id.
  Order detail returns the selected immutable `DeliveryAddress` snapshot.
  Invalid ownership, missing, or deleted addresses return `DELIVERY_ADDRESS_NOT_FOUND`.
- Test results: solution build passed (0 errors, 23 existing warnings); Orders unit tests 545/545;
  Assistant unit tests 83/83; related Orders PostgreSQL integration tests 9/9; Assistant HTTP/tool
  integration tests 6/6; EF model has no pending changes.

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
- 2026-08-01: Require an explicit active restaurant-owned address at confirmation; do not infer a
  default delivery address.
- 2026-08-01: Store six nullable scalar snapshot columns without a foreign key so drafts and legacy
  orders remain valid and confirmed orders survive source address edits or deletion.
- 2026-08-01: Capture the address inside the existing serializable confirmation transaction so stock,
  credit, order status, and the snapshot commit or roll back together.
- 2026-08-01: GAP-02 initial commit `22a9cbc` received a P1 review finding: Assistant confirmation
  must bind the client-selected delivery address in the safety gate. GAP-03 remains blocked until the
  follow-up fix is committed.
- 2026-08-01: P1 fixed by moving `deliveryAddressId` out of LLM tool arguments into server-injected
  context, requiring it in the gate, and echoing it with the pending preview.
- 2026-08-01: GAP-02 follow-up committed as `a8eb93d`; GAP-03 is unblocked.
- 2026-08-01: GAP-03 uses the active market hub as the origin, falls back to market coordinates,
  and uses the farthest Haversine distance for a consolidated order; no routing dependency is added.
- 2026-08-01: Product VAT supports `KCT`, `0`, `5`, `8`, and `10`; the confirmation snapshot, not
  later catalog state, is the source for invoicing.
- 2026-08-01: Delivery fee defaults to 5,000 VND/km and is managed through operational settings.
- 2026-08-01: GAP-03 review fixed partial hub coordinates so the origin falls back as one complete
  market coordinate pair instead of mixing a hub latitude/longitude with its market counterpart.
- 2026-08-01: GAP-04 reuses procurement purchase confirmation and extends only the handover event;
  price-band approval and credit reconciliation remain outside this GAP because its acceptance
  criteria require recording actuals and deterministic shortage allocation, not repricing debt.
- 2026-08-01: Shortages use two-decimal largest-remainder allocation with stable order/item ID
  tie-breaks. Reservation consumption, shortfall release, actual snapshots, and order transitions
  share one serializable transaction.
- 2026-08-01: GAP-04 review found that post-commit domain dispatch could leave Procurement handed
  off while Orders rolled back, or broadcast uncommitted statuses. Handover now invokes a dedicated
  Orders finalizer inside the shared serializable PostgreSQL transaction, captures status events,
  commits Procurement/Orders/stock together, then publishes captured events once after commit.

## GAP-03 — MOQ, VAT, and distance delivery fee

### Scope and API contract

- Store a configurable minimum order quantity and VAT rate on each catalog product.
- At confirmation, aggregate quantity by market product and reject any line below MOQ.
- Snapshot each line's VAT code/rate and the order's subtotal, VAT, delivery distance, delivery fee,
  and final total in the existing serializable transaction.
- Calculate straight-line Haversine distance from the market's active hub (falling back to the market
  coordinates) to the selected delivery address; one consolidated order uses the farthest origin.
- Use the admin-managed delivery-fee-per-kilometre setting and include VAT plus delivery fee in the
  credit check, charge, refund, order detail, and confirmation preview.

### Acceptance criteria

- [x] Product create/update validates and exposes MOQ and supported VAT codes.
- [x] Confirmation rejects aggregate quantities below MOQ without reserving stock or charging credit.
- [x] VAT uses the product rate at confirmation and remains unchanged after the product changes.
- [x] Preview and confirmation return the same subtotal, VAT, distance, fee, and final total.
- [x] Missing coordinates reject confirmation deterministically without partial mutation.
- [x] Credit failure rolls back pricing snapshots; cancellation refunds the VAT- and fee-inclusive total once.
- [x] Solution build, Catalog/Orders/Invoicing unit tests, and related PostgreSQL integration tests pass.

### Change record

- Files changed:
  - `src/FreshFlow.API/Assistant/Tools/ToolDefinitions.cs`
  - `src/FreshFlow.API/Controllers/AdminController.cs`
  - `src/FreshFlow.API/Controllers/OrdersController.cs`
  - `src/FreshFlow.API/Controllers/ProductsController.cs`
  - `src/FreshFlow.API/Extensions/ErrorExtensions.cs`
  - `src/FreshFlow.Infrastructure.Persistence/Migrations/20260801151302_AddOrderCommercialTerms.cs`
  - `src/FreshFlow.Infrastructure.Persistence/Migrations/20260801151302_AddOrderCommercialTerms.Designer.cs`
  - `src/FreshFlow.Infrastructure.Persistence/Migrations/AppDbContextModelSnapshot.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Commands/Products/Create/CreateProductCommand.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Commands/Products/Create/CreateProductCommandHandler.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Commands/Products/Create/CreateProductCommandValidator.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Commands/Products/Update/UpdateProductCommand.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Commands/Products/Update/UpdateProductCommandHandler.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Commands/Products/Update/UpdateProductCommandValidator.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Dtos/ProductDto.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Application/Mappings/ProductMappings.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Domain/Entities/Product.cs`
  - `src/Modules/Catalog/FreshFlow.Catalog.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
  - `src/Modules/Invoicing/FreshFlow.Invoicing.Infrastructure/CrossModule/OrderInvoiceRowConfiguration.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/IMarketProductReader.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/IOperationalSettingsRepository.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/ConfirmOrder/ConfirmOrderCommandHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/UpdateOperationalSettings/UpdateOperationalSettingsCommand.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/UpdateOperationalSettings/UpdateOperationalSettingsCommandHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Commands/UpdateOperationalSettings/UpdateOperationalSettingsCommandValidator.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OperationalSettingsDto.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OrderDto.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OrderDtoMapper.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OrderItemDto.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Queries/GetOperationalSettings/GetOperationalSettingsQueryHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Queries/PreviewOrderConfirmation/OrderConfirmationPreviewDto.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Queries/PreviewOrderConfirmation/PreviewOrderConfirmationQuery.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Queries/PreviewOrderConfirmation/PreviewOrderConfirmationQueryHandler.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderConfirmationEvaluator.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderPricingCalculator.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/OperationalSettings.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/OrderItem.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/CrossModule/MarketProductReader.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/CrossModule/MarketProductRow.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/CrossModule/MarketProductRowConfiguration.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/OperationalSettingsConfiguration.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/OrderItemConfiguration.cs`
  - `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Repositories/OperationalSettingsRepository.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Invoicing/InvoicingPostgresTests.cs`
  - `tests/Integration/FreshFlow.IntegrationTests/Orders/AtomicStockReservationEndpointTests.cs`
  - `tests/Unit/FreshFlow.Catalog.UnitTests/Products/CreateProductCommandValidatorTests.cs`
  - `tests/Unit/FreshFlow.Catalog.UnitTests/Products/UpdateProductCommandValidatorTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Commands/CancelOrderCommandHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Commands/ConfirmOrderCommandHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Commands/UpdateOperationalSettingsCommandHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Queries/PreviewOrderConfirmationControllerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Queries/PreviewOrderConfirmationQueryHandlerTests.cs`
  - `tests/Unit/FreshFlow.Orders.UnitTests/Services/OrderPricingCalculatorTests.cs`
- Migration: `20260801151302_AddOrderCommercialTerms` adds product MOQ, the delivery-fee setting,
  order-level pricing totals, and line-level VAT snapshots; it backfills legacy order subtotal from
  `TotalAmount`.
- API contract: product create/update and DTOs expose `minimumOrderQuantity` and `vatRate`;
  operational settings expose `deliveryFeePerKm`; confirmation request is unchanged; confirmation,
  order detail, and preview expose subtotal, VAT, distance, delivery fee, and final total. Confirmation
  preview requires `deliveryAddressId`. MOQ/coordinate/pricing validation errors return HTTP 422.
- Test results: solution build passed (0 errors, 21 existing warnings); Catalog unit tests 187/187;
  Orders unit tests 551/551; Invoicing unit tests 36/36; Assistant unit tests 83/83; related Orders and
  Invoicing PostgreSQL integration tests 14/14; Assistant HTTP/tool integration tests 6/6; EF model has
  no pending changes.

## GAP-04 — Actual purchase price and pro-rata shortage allocation

### Scope and API contract

- Reuse the existing procurement purchase-confirmation request and batch-item actual quantity/price.
- Include purchased actuals in the handover integration event and snapshot the actual unit price on
  affected order items.
- When purchased quantity is short, allocate it proportionally across covered order items at two
  decimal places using largest remainder; break ties by order ID then order-item ID.
- Consume only the purchased portion of the PostgreSQL reservation and release the shortfall in the
  same serializable transaction before advancing covered orders to `AtHub`.
- Repeated handover events are no-ops and cannot consume, release, or allocate twice.

### Acceptance criteria

- [x] Full purchase records ordered quantity and actual unit price on every affected order item.
- [x] A shortage across multiple orders is allocated pro-rata, totals exactly the purchased quantity,
  and produces the same result regardless of input order.
- [x] Zero-purchase/unavailable items allocate zero and release their whole reservation.
- [x] Stock consumption, reservation release, order actuals, and status transitions roll back together.
- [x] A repeated handover event does not mutate stock or order actuals twice.
- [x] Order detail and invoicing expose the actual purchase price snapshot when present.
- [x] Solution build, Procurement/Orders/Invoicing unit tests, and related PostgreSQL integration tests pass.

### Change record

- Files changed:
  - src/Shared/FreshFlow.Contracts/ProcurementBatchHandedOffIntegrationEvent.cs
  - src/Modules/Procurement/FreshFlow.Procurement.Domain/Events/ProcurementBatchHandedOffDomainEvent.cs
  - src/Modules/Procurement/FreshFlow.Procurement.Domain/Entities/ProcurementBatch.cs
  - src/Modules/Procurement/FreshFlow.Procurement.Application/EventHandlers/ProcurementBatchHandedOffDomainEventHandler.cs
  - src/Modules/Procurement/FreshFlow.Procurement.Application/Abstractions/IProcurementBatchRepository.cs
  - src/Modules/Procurement/FreshFlow.Procurement.Application/Commands/HandoverBatch/HandoverBatchCommandHandler.cs
  - src/Modules/Procurement/FreshFlow.Procurement.Infrastructure/Repositories/ProcurementBatchRepository.cs
  - src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs
  - src/Modules/Orders/FreshFlow.Orders.Domain/Entities/OrderItem.cs
  - src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/IOrderRepository.cs
  - src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OrderDtoMapper.cs
  - src/Modules/Orders/FreshFlow.Orders.Application/Dtos/OrderItemDto.cs
  - src/Modules/Orders/FreshFlow.Orders.Application/EventHandlers/ProcurementBatchHandedOffIntegrationEventHandler.cs
  - src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/OrderItemConfiguration.cs
  - src/Modules/Orders/FreshFlow.Orders.Infrastructure/Repositories/OrderRepository.cs
  - src/Modules/Orders/FreshFlow.Orders.Infrastructure/DependencyInjection.cs
  - src/Modules/Invoicing/FreshFlow.Invoicing.Application/Abstractions/IOrderInvoiceReader.cs
  - src/Modules/Invoicing/FreshFlow.Invoicing.Infrastructure/CrossModule/OrderInvoiceRowConfiguration.cs
  - src/FreshFlow.Infrastructure.Persistence/Migrations/20260801155249_AddOrderProcurementActualPrice.cs
  - src/FreshFlow.Infrastructure.Persistence/Migrations/20260801155249_AddOrderProcurementActualPrice.Designer.cs
  - src/FreshFlow.Infrastructure.Persistence/Migrations/AppDbContextModelSnapshot.cs
  - tests/Unit/FreshFlow.Orders.UnitTests/EventHandlers/ProcurementBatchHandedOffIntegrationEventHandlerTests.cs
  - tests/Unit/FreshFlow.Procurement.UnitTests/Domain/ProcurementBatchTests.cs
  - tests/Unit/FreshFlow.Procurement.UnitTests/EventHandlers/ProcurementBatchHandedOffDomainEventHandlerTests.cs
  - tests/Unit/FreshFlow.Procurement.UnitTests/Commands/HandoverBatchCommandTests.cs
  - tests/Integration/FreshFlow.IntegrationTests/Orders/AtomicStockReservationEndpointTests.cs
  - tests/Integration/FreshFlow.IntegrationTests/Procurement/ProcurementBatchEndpointTests.cs
  - tests/Integration/FreshFlow.IntegrationTests/Invoicing/InvoicingPostgresTests.cs
- Migration: 20260801155249_AddOrderProcurementActualPrice adds nullable
  order_items.ActualUnitPrice; EF reports no pending model changes.
- API contract: procurement purchase and handover HTTP requests are unchanged; the internal handover
  event now carries per-product actual quantity/price. Order detail adds nullable
  items[].actualUnitPrice; invoicing prefers it over the confirmation price snapshot.
- Test results: solution build passed (0 errors, 23 existing warnings); Orders unit tests 553/553;
  Procurement unit tests 124/124; Invoicing unit tests 36/36; related Orders/Invoicing PostgreSQL
  integration tests 16/16; Procurement endpoint PostgreSQL integration tests 8/8, including failed
  finalization rollback and successful retry. Scoped solution format passes while excluding only
  BOMs in the already-committed GAP-02/GAP-03 migration files; the GAP-04 migration BOM was removed.
