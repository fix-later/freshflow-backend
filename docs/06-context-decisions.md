# FreshFlow — Context & Business Model Decisions

**Version:** 1.0
**Date:** 2026-05-31
**Project:** FreshFlow – Intermediary Platform for Food Procurement and Logistics Optimization

---

## Decision Log

### DEC-001: Procurement Agent Model over Marketplace Model

**Decision:** Market Agents are internal FreshFlow employees, not external vendors.

**Rationale:**
- Guarantees price reliability — FreshFlow controls price data quality, not external vendors with varying incentives
- Restaurants see prices BEFORE ordering (unlike a pure "buying agent" model where prices are unknown until purchase)
- Preserves technical complexity of WebSocket + VRP for Capstone evaluation
- Avoids dependency on vendor willingness to maintain accurate, real-time price data

**Trade-off:** Higher operating cost (FreshFlow pays Agent salaries) vs. better quality control and a more trustworthy product.

**Impact on tech:** No change to the technical stack. The price update endpoint (`PATCH`) and SignalR broadcast work identically. The only change is WHO triggers the update — an internal employee rather than an external vendor.

**Role enum:** `MARKET_AGENT` is the canonical new value. `KIOSK_STAFF` is retained as a backward-compatible alias and will be removed in a future migration.

---

### DEC-002: Payment via B2B Credit / Công nợ (NOT a per-order gateway)

**Decision:** Restaurants do **not** pay through an online payment gateway per order. Instead, each
restaurant has a **credit account (công nợ)** with a credit limit set by Admin. Confirming an order
draws down the available credit; outstanding debt is settled out-of-band (bank transfer / cash) and
recorded by Admin.

> **Supersedes the earlier gateway decision.** An earlier revision of this document specified a
> per-order Vietnamese payment gateway (VNPay/MoMo/ZaloPay). That approach was reversed during the
> Orders module implementation in favour of the B2B credit model, which matches how F&B wholesale
> procurement actually settles in Vietnam. `FR-ORD-008` in `01-requirements-spec.md` still describes
> the legacy gateway flow and is superseded by this decision.

**Rationale:**
- Matches real B2B procurement: restaurants buy daily on credit and settle periodically, rather than
  paying a gateway fee on every order.
- No dependency on third-party gateway sandbox/credentials for the Capstone demo.
- The discrepancy refund flow (Hub Staff flags missing goods) becomes a **credit refund** — the
  outstanding balance is reduced — which is simpler and gateway-independent.

**Payment timing:** At order CONFIRMATION — restaurant calls `POST /api/orders/{orderId}/confirm`.
The handler checks `CanChargeAsync` (available credit ≥ order total) and, on success, charges the
order total against the restaurant's outstanding balance.

**Model (as implemented in `Modules/Orders`):**
- `RestaurantCredit` aggregate: `CreditLimit`, `OutstandingBalance`, `AvailableCredit = CreditLimit − OutstandingBalance`.
  Operations: `Charge`, `Settle`, `Refund`, `SetCreditLimit` (limit may not drop below outstanding balance).
- `CreditTransaction` ledger records every charge/settlement/refund.
- `OrderPaymentStatus` enum (independent of `OrderStatus`): `NotApplicable → Outstanding → Settled` (or `Waived` on cancel).
- Admin endpoints: set credit limit, settle credit; restaurant/admin can read balance and transactions.

**Confirmation fails** with a domain error if the order total would exceed the restaurant's available
credit — there is no gateway redirect, payment token, or webhook.

**Price Band:** If actual purchase price differs ≤ 10% from `locked_unit_price` → auto-adjust the
charged amount, no user action. If > 10% → notify restaurant, wait 30 minutes, then auto-confirm to
keep delivery schedule. If actual price is LOWER → restaurant is charged the lower price automatically.

---

### DEC-003: Single Restaurant Actor

**Decision:** Restaurant Manager and Restaurant Staff are merged into one **Restaurant** actor.

**Rationale:** Most F&B SMEs in Vietnam have one person doing both management and daily ordering at this scale. Splitting into two sub-roles adds UX complexity with little v1 benefit. RBAC can introduce permission sub-levels within the Restaurant role in v2 if needed.

---

### DEC-004: Hub Staff and Driver as Explicit Roles

**Decision:** Hub Staff and Driver are distinct roles with dedicated mobile interfaces.

**Rationale:** Each has a completely non-overlapping set of use cases:
- Hub Staff needs scan/QC interface + discrepancy flagging
- Driver needs route/map interface + delivery confirmation

Merging these roles would create a bloated, confusing UX and complicate RBAC. The data model already accommodates them via the `user_role` enum.

---

### DEC-005: Admin / Operations Manager Merged

**Decision:** System Admin and Operations Manager are treated as one role (`ADMIN`).

**Rationale:** At Capstone/early-startup scale, these are the same person. The ADMIN role has full system access. If the business grows and these responsibilities need to be split, RBAC sub-roles can be introduced in v2 without a schema change.

---

### DEC-006: Order Auto-Batching at 22:00 Cutoff

**Decision:** The system automatically batches all `CONFIRMED` orders at the 22:00 daily cutoff time. Admin can also trigger the same auto-batching service manually through `POST /api/v1/admin/order-groups/auto-batch`.

**Old behavior:** Admin manually creates `order_groups` by selecting orders.

**New behavior:** A system cron job at 22:00 groups all `CONFIRMED` orders by source market, creating `OrderGroup` entities and transitioning order status to `BATCHED` automatically. (Grouping by delivery zone was specified in FR-ORD-005 but is not implemented — there is no restaurant-to-zone link.)

**Rationale:** Manual batching doesn't scale, introduces human error, and creates an operational bottleneck at a fixed time each night. System auto-batching is deterministic and auditable.

**Configuration:** Cutoff time is configurable via `system_config` key `daily_order_cutoff_time` (default `22:00`, timezone `Asia/Ho_Chi_Minh`). Admin can still manually adjust batches after auto-generation. Manual trigger supports `targetDate`, `dryRun`, and `force` and must be idempotent so repeated runs do not create duplicate active batches.

---

### DEC-007: Price Lock Snapshot Pattern

**Decision:** `order_items.locked_unit_price` stores the price as a snapshot at `CONFIRMED` status transition time.

**Why not a FK reference to `price_snapshots`?**
Price snapshots are a high-volume append-only table (currently a plain table with ordinary indexes; monthly range partitioning is a target design, not implemented). FK references across the orders boundary would complicate cross-context queries and violate the bounded context principle. A scalar snapshot is simpler, faster to read, and immune to cascade effects.

**Price Band Rule:** Tolerate ±10% deviation between `locked_unit_price` and actual purchase price. Wider deviation requires restaurant re-confirmation (30-min window, then auto-confirm). Configurable via `system_config` key `price_band_tolerance_percent`.

---

### DEC-008: Hub QC Gate Before Driver Dispatch

**Decision:** Missing or damaged goods are flagged by Hub Staff BEFORE the Driver dispatches — not discovered at delivery.

**Rationale:** At 7AM the restaurant is about to open for breakfast prep. Discovering missing goods at the door (7AM) is too late for re-procurement. Flagging at the hub (5:00–6:00 AM) gives a 1-hour window to notify the restaurant, initiate a refund, and potentially source replacement items.

**Flow:**
1. Hub Staff scans inbound goods (5:00 AM)
2. If missing/damaged → flag via app, system sends `HubDiscrepancyNotification` to restaurant and queues refund
3. All discrepancies resolved/acknowledged before Driver departs
4. Driver dispatches with accurate manifest

---

### DEC-009: Microservice-Ready Monolith (Bounded Context Separation)

**Decision:** Design 11 bounded contexts with clean separation in a single PostgreSQL database and single deployable unit.

**Rationale:** Enables future microservice extraction without major refactoring. Key rules enforced now:
- No FK constraints across module boundaries (cross-context references are IDs only)
- Snapshot pattern for immutable cross-context data (e.g., `product_name_snapshot`, `locked_unit_price`)
- Integration events for async cross-context updates

**Current state:** Modular monolith, single `AppDbContext`, single deployment.

**Future state:** Each bounded context → independent service with own database. The `payment` module is already designed as a separate context (no FK from `payments` to `orders`).

---

## Out of Scope (Explicit)

These features will NOT be built in v1 under any circumstances:

| Feature | Reason |
|---------|--------|
| Consumer-facing (B2C) features | FreshFlow is B2B only. No public marketplace, consumer checkout, or search engine indexing. |
| Accounting / ERP integration | No webhooks, EDI, or API integration with restaurant POS or accounting systems. Financial reporting is offline. |
| Cold-chain IoT sensor integration | No temperature/humidity tracking from market to hub to restaurant. Deferred to v2. |
| Multi-city expansion beyond HCMC | Platform is scoped to Ho Chi Minh City for v1. Data model supports future expansion but no multi-city data is built. |
| External vendor self-registration | Market Agents are internal staff. External market vendors cannot create accounts or manage prices. |
| Real-time GPS tracking history > 30 days | Driver status updates (ARRIVED/DELIVERED) are in scope; continuous GPS stream and long-term telematics are not. |
| Advanced fleet management | Vehicle registration is in scope. Fleet optimization, maintenance scheduling, and telematics are out of scope. |

---

## Terminology Alignment

| Old Term | New Term / Clarification | Notes |
|----------|--------------------------|-------|
| Kiosk Staff | **Market Agent** | Internal FreshFlow employee, not external vendor. `KIOSK_STAFF` enum retained as alias. |
| Order Group (Admin-created) | **Procurement Batch** (auto-generated) | System creates at 22:00 cutoff, Admin can adjust. |
| `PENDING` status | `DRAFT` | Order lifecycle starts at DRAFT. |
| "Payment out of scope" / "per-order gateway" | **Payment via B2B credit (công nợ)** | Restaurant credit account with Admin-set limit; confirm draws down credit, settled out-of-band. Supersedes the per-order VNPay/MoMo/ZaloPay flow (see DEC-002). |
| Logistics Operator | **Admin** | No separate Logistics Operator role; Admin handles logistics scheduling. |

---

*End of FreshFlow Context & Business Model Decisions v1.0*

*Prepared by: Requirements Update — FreshFlow Platform 2026-05-31*
