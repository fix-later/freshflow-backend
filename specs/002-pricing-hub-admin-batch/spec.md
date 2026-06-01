# Feature Specification: V1 Scope — Simplified Pricing Broadcast & Admin Manual Order Batching

**Feature Branch**: `002-pricing-hub-admin-batch`
**Created**: 2026-05-31
**Status**: Draft
**Input**: User description: "FreshFlow wholesale market food procurement and logistics platform. Bỏ significant price change alert khỏi v1; PricingHub chỉ gửi PriceUpdated; thêm Admin endpoint POST /api/v1/admin/order-groups/auto-batch để trigger thủ công cùng logic gom đơn tự động 22:00."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Admin Manually Triggers Order Batching (Priority: P1)

As a Platform Admin, I want to manually trigger the nightly order-batching process at any time,
so that I can recover from a missed 22:00 run, perform pre-release testing, or batch orders
for a specific past date without waiting for the scheduled job.

**Why this priority**: The 22:00 scheduled job is the primary batching mechanism, but operational
incidents (server downtime, misconfiguration) can cause missed runs. Admins need a safe recovery
path that is idempotent and produces no duplicate batches.

**Independent Test**: Create a set of CONFIRMED orders, call the endpoint, verify they move to
BATCHED status and are assigned to OrderGroup records — without any 22:00 schedule dependency.

**Acceptance Scenarios**:

1. **Given** one or more orders are in CONFIRMED status and not yet assigned to a batch,
   **When** an Admin calls `POST /api/v1/admin/order-groups/auto-batch` (no body),
   **Then** the system groups eligible orders by delivery zone and source market,
   transitions them to BATCHED status, and returns a summary with `createdBatchCount`,
   `batchedOrderCount`, and `skippedOrderCount`.

2. **Given** the endpoint is called twice for the same `targetDate`,
   **When** no new CONFIRMED orders exist after the first call,
   **Then** the second call returns `createdBatchCount: 0`, `batchedOrderCount: 0`,
   and no duplicate OrderGroup records are created (idempotent behaviour).

3. **Given** an Admin provides `dryRun: true` in the request body,
   **When** the endpoint is called,
   **Then** the system returns the projected batch summary without writing any database changes.

4. **Given** a non-Admin authenticated user calls the endpoint,
   **When** the request is processed,
   **Then** the system returns HTTP 403 Forbidden.

---

### User Story 2 — Real-Time Price Updates for Restaurant Users (Priority: P1)

As a Restaurant user subscribed to a market's pricing channel, I want to receive immediate
notifications whenever a kiosk staff member updates a product price or quantity, so that I can
make timely procurement decisions based on current market conditions.

**Why this priority**: Real-time pricing is a core value proposition of FreshFlow. All subscribed
restaurants must see price changes within 500 ms of the update being saved.

**Independent Test**: Update a price via the kiosk staff endpoint; measure elapsed time until
the `PriceUpdated` event is received by a connected restaurant client in the same market group.

**Acceptance Scenarios**:

1. **Given** a restaurant user is connected to the PricingHub and subscribed to `market:{marketId}`,
   **When** a kiosk staff member updates the price or quantity of a product in that market,
   **Then** the PricingHub broadcasts a `PriceUpdated` event to the `market:{marketId}` group
   within 500 ms, containing: `marketId`, `productId`, `marketProductId`, `newPrice`,
   `newQuantity`, `previousPrice`, `changePercent`, and `updatedAt`.

2. **Given** a restaurant user is connected but subscribed to a different market,
   **When** a price update occurs in another market,
   **Then** the user receives no notification (group isolation enforced).

3. **Given** the PricingHub is operational,
   **When** any price update occurs,
   **Then** only the `PriceUpdated` event type is sent — no threshold-based or significance-filtered
   alert events are ever emitted by this hub in v1.

---

### Edge Cases

- What happens when `targetDate` is in the future? System rejects with a clear validation error.
- What happens when no CONFIRMED orders exist at all? Endpoint returns success with all counts at zero.
- What happens when a PricingHub subscriber disconnects mid-broadcast? Event is silently dropped; reconnecting clients must request current prices via REST.
- What happens when the 22:00 scheduled job and an Admin trigger run concurrently? The idempotency guarantee prevents duplicate batches; concurrent calls are safe.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST NOT implement significant price change alerts or threshold-based
  pricing notifications in v1. The `price_band_tolerance_percent` configuration key exists
  but is reserved for future use only.
- **FR-002**: PricingHub MUST broadcast only the `PriceUpdated` event type to `market:{marketId}`
  groups; no other event types are permitted in v1.
- **FR-003**: The `POST /api/v1/admin/order-groups/auto-batch` endpoint MUST be restricted to
  users with the Admin role; all other roles receive HTTP 403.
- **FR-004**: The endpoint MUST accept an optional `targetDate` (ISO 8601 date) parameter;
  when omitted, the current calendar date in Vietnam timezone (`Asia/Ho_Chi_Minh`) is used.
- **FR-005**: The endpoint MUST accept an optional `dryRun` boolean parameter; when `true`,
  no database writes occur and the projected outcome is returned.
- **FR-006**: The batching logic MUST be the same shared service used by the scheduled 22:00 job —
  no separate implementation.
- **FR-007**: The operation MUST be idempotent: calling it multiple times for the same date
  MUST NOT create duplicate `OrderGroup` records or re-batch already-BATCHED orders.
- **FR-008**: The endpoint MUST return a summary including `createdBatchCount`, `batchedOrderCount`,
  `skippedOrderCount`, and per-group details (`deliveryZone`, `sourceMarketId`).

### Key Entities

- **OrderGroup**: A batch of orders grouped by delivery zone and source market, with status
  lifecycle (open → locked → dispatched → completed).
- **Order**: A restaurant's purchase order; eligible for batching when in CONFIRMED status
  and not yet assigned to an active OrderGroup.
- **PriceUpdated Event**: Real-time SignalR message carrying current price, quantity, and
  change percent for a specific market product.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin can trigger order batching at any time; the endpoint responds with batch
  results within 5 seconds for up to 500 simultaneous CONFIRMED orders.
- **SC-002**: Calling the auto-batch endpoint twice in succession for the same date produces
  zero duplicate OrderGroup records (100% idempotency).
- **SC-003**: Price updates reach all subscribed restaurant users within 500 ms of the
  database write completing, under normal load.
- **SC-004**: Zero threshold-based or significance-filtered price alert events are emitted
  by PricingHub during the entire v1 lifetime.
- **SC-005**: The admin endpoint is inaccessible to non-Admin roles; unauthorized access
  attempts return HTTP 403 with no order data exposed.

## Assumptions

- Significant price change alert functionality is explicitly deferred to post-v1 with no
  committed timeline; the `price_band_tolerance_percent` system config key is preserved in
  the schema as a placeholder only.
- The `OrderBatchingService` is already designed as a shared, injectable service invoked by
  both the scheduled background job and the admin endpoint — no duplication of logic required.
- Admin authentication and role-based authorization are enforced by existing middleware;
  this spec does not redefine the auth mechanism.
- "Vietnam timezone" means `Asia/Ho_Chi_Minh` (UTC+7); `targetDate` defaults to the current
  date in this timezone when not explicitly provided.
- The endpoint is available in v1 alongside the 22:00 scheduled job — both invoke the same
  underlying service.
