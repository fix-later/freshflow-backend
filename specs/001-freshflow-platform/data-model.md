# Data Model: FreshFlow Platform

**Branch**: `001-freshflow-platform` | **Date**: 2026-05-11
**Source of truth**: `docs/03-database-schema.md` (DDL) + `docs/02-system-architecture.md`

> This document captures the logical data model as input to implementation.
> The canonical DDL lives in `docs/03-database-schema.md`. Reproduce it faithfully in
> EF Core `IEntityTypeConfiguration<T>` classes — do not diverge without a schema amendment.

---

## Module Ownership

| Module | Entities Owned |
|--------|---------------|
| Auth | `users`, `refresh_tokens`, `user_market_assignments` |
| Pricing | `markets`, `products`, `market_products`, `price_snapshots`, `system_config` |
| Orders | `restaurants`, `orders`, `order_items`, `order_groups`, `scheduled_orders` |
| Logistics | `vehicles`, `delivery_routes`, `deliveries` |
| Hub | `hubs`, `hub_inventory`, `hub_inbound_events`, `hub_outbound_events`, `cross_dock_transfers` |
| Analytics | `analytics_aggregations`, `export_jobs` |
| Notifications | `notifications` |

---

## Auth Module

### users
Primary account table for all user types.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | UUID PK | `gen_random_uuid()` | Exposed in API as user identifier |
| `email` | TEXT | NOT NULL, UNIQUE | Case-sensitive; validated on create |
| `password_hash` | TEXT | NOT NULL | BCrypt hash, work factor ≥ 12 |
| `role` | user_role ENUM | NOT NULL | `admin`, `kiosk_staff`, `restaurant` |
| `is_active` | BOOLEAN | NOT NULL, DEFAULT true | Fast active check; false = deactivated |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Updated on any field change |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL = active; soft delete |

**Indexes**: `users_email_unique` (unique on email), `idx_users_role` (for admin user lists).

### refresh_tokens
Append-only token audit log. One row per issued token. Never updated.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `user_id` | UUID FK → users.id | ON DELETE CASCADE |
| `token_hash` | TEXT | UNIQUE; bcrypt hash of the raw random token |
| `family_id` | UUID | Groups tokens from the same login session for reuse-invalidation |
| `expires_at` | TIMESTAMPTZ | 7 days after issuance |
| `revoked_at` | TIMESTAMPTZ | NULL = still valid; set to NOW() on rotation or logout |
| `replaced_by_token_id` | UUID FK → refresh_tokens.id | Audit chain; SET NULL on delete |
| `created_at` | TIMESTAMPTZ | |

**Notes**: No `updated_at` or `deleted_at` — this is append-only. Queried by `token_hash` index.

### user_market_assignments
Maps Kiosk Staff to their allowed markets.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `user_id` | UUID FK → users.id | ON DELETE CASCADE |
| `market_id` | UUID FK → markets.id | ON DELETE CASCADE |
| `assigned_by` | UUID FK → users.id | Admin who made the assignment; SET NULL on delete |
| `created_at` / `updated_at` | TIMESTAMPTZ | |

**Constraint**: `UNIQUE (user_id, market_id)` — no duplicate assignments.

---

## Pricing Module

### markets
Wholesale market registry. Three seeded: Hoc Mon, Binh Dien, Thu Duc.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `name` | TEXT | |
| `location` / `address` | TEXT | Human-readable description |
| `latitude` / `longitude` | NUMERIC(9,6) | Coordinates for route calculation |
| `is_active` | BOOLEAN | false = no new price updates accepted |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | Soft delete |

### products
System-wide product catalog. Shared across all markets.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `name` | TEXT | |
| `category` | TEXT | Grouping for display |
| `unit` | TEXT | e.g., "kg", "bunch", "crate" |
| `description` | TEXT | Optional |
| `created_by` | UUID FK → users.id | SET NULL on delete |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | Soft delete = product deactivated |

### market_products
Join entity: current price and quantity per (market, product) pair.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `market_id` | UUID FK → markets.id | ON DELETE CASCADE |
| `product_id` | UUID FK → products.id | ON DELETE CASCADE |
| `current_price` | NUMERIC(12,2) | CHECK ≥ 0; updated by Kiosk Staff |
| `current_quantity` | INTEGER | CHECK ≥ 0; physical available quantity |
| `reserved_quantity` | INTEGER | CHECK ≥ 0, DEFAULT 0; soft-reservation total |
| `updated_by` | UUID FK → users.id | Last Kiosk Staff to touch this row |
| `created_at` / `updated_at` | TIMESTAMPTZ | `updated_at` is the optimistic-concurrency guard |

**Available quantity** = `current_quantity - reserved_quantity` (computed in application layer).

**Constraint**: `UNIQUE (market_id, product_id)`.

### price_snapshots
Immutable append-only price history. **Partitioned by month** on `recorded_at`.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID | NOT NULL; part of PK on each partition |
| `market_product_id` | UUID FK → market_products.id | ON DELETE CASCADE |
| `price` | NUMERIC(12,2) | CHECK ≥ 0 |
| `quantity` | INTEGER | CHECK ≥ 0 |
| `recorded_by` | UUID FK → users.id | SET NULL on user delete |
| `recorded_at` | TIMESTAMPTZ | Partition key |

**Partitioning**: `PARTITION BY RANGE (recorded_at)`. Monthly child tables:
`price_snapshots_y2026m05`, `price_snapshots_y2026m06`, etc. Default partition for overflow.
`PartitionMaintenanceJob` creates next month's partition on the 25th.

**No soft delete, no updated_at** — immutable by design.

### system_config
Key-value store for Admin-configurable runtime parameters.

Key values used at runtime:
- `significant_price_threshold_percent` (default: `"5.00"`)

---

## Orders Module

### restaurants
One profile per Restaurant user. Must be approved by Admin before placing orders.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `user_id` | UUID FK → users.id | UNIQUE; ON DELETE CASCADE |
| `name` | TEXT | Restaurant display name |
| `address` | TEXT | |
| `latitude` / `longitude` | NUMERIC(9,6) | Used for route calculation |
| `is_approved` | BOOLEAN | false = PENDING_APPROVAL; cannot place orders |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | |

### order_groups
Groups confirmed orders for batch logistics dispatch.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `name` | TEXT | Optional admin label |
| `status` | order_group_status ENUM | `open`, `locked`, `dispatched`, `completed` |
| `grouped_by` | UUID FK → users.id | Admin who created the group |
| `total_orders` | INTEGER | Denormalized count; maintained by app |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | |

### orders
Aggregate root of the order lifecycle.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `restaurant_id` | UUID FK → restaurants.id | ON DELETE RESTRICT |
| `order_group_id` | UUID FK → order_groups.id | NULL = not grouped; ON DELETE SET NULL |
| `scheduled_order_id` | UUID FK → scheduled_orders.id | NULL = one-off order |
| `status` | order_status ENUM | `pending`, `confirmed`, `processing`, `ready_for_pickup`, `in_transit`, `delivered`, `cancelled` |
| `total_amount` | NUMERIC(15,2) | Denormalized; maintained by app on line-item add |
| `notes` | TEXT | Optional restaurant notes |
| `cancelled_at` | TIMESTAMPTZ | Set when status → cancelled |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | |

**State transitions allowed**:
- `pending → confirmed → processing → ready_for_pickup → in_transit → delivered`
- `pending → cancelled` or `confirmed → cancelled`
- All other transitions are invalid (HTTP 422)

### order_items
Line items within an order.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `order_id` | UUID FK → orders.id | ON DELETE CASCADE |
| `market_product_id` | UUID FK → market_products.id | ON DELETE RESTRICT |
| `quantity` | INTEGER | CHECK > 0 |
| `unit_price` | NUMERIC(12,2) | Price at order creation time; immutable |
| `subtotal` | NUMERIC(15,2) | GENERATED ALWAYS AS (quantity * unit_price) STORED |

### scheduled_orders
Recurring order templates.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `restaurant_id` | UUID FK → restaurants.id | ON DELETE CASCADE |
| `recurrence_type` | TEXT | `daily` or `weekly` |
| `day_of_week` | INTEGER | 0=Sun…6=Sat; NULL for daily |
| `run_time` | TIME | Local time in Asia/Ho_Chi_Minh |
| `first_run_at` | TIMESTAMPTZ | First execution UTC time |
| `last_executed_at` | TIMESTAMPTZ | Used by job for idempotency check |
| `template_items` | JSONB | Line item definitions (product_id, market_id, quantity) |
| `is_active` | BOOLEAN | false = cancelled |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | |

---

## Logistics Module

### vehicles
Fleet registry.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `plate_number` | TEXT | UNIQUE |
| `capacity_kg` | NUMERIC(10,2) | |
| `vehicle_type` | TEXT | `van`, `truck`, `motorbike` |
| `is_available` | BOOLEAN | false = in use or inactive |
| `is_active` | BOOLEAN | |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | |

### delivery_routes
Calculated route with ordered stop list stored as JSONB.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `vehicle_id` | UUID FK → vehicles.id | Assigned vehicle |
| `optimization_criterion` | TEXT | `distance`, `time`, `cost` |
| `route_type` | TEXT | `direct`, `hub_relay` |
| `total_distance_km` | NUMERIC(10,2) | |
| `total_duration_min` | INTEGER | |
| `estimated_cost_vnd` | NUMERIC(15,2) | |
| `route_metadata` | JSONB | Ordered stop list: [{entityType, entityId, lat, lng, estimatedArrivalAt, estimatedDepartureAt}] |
| `cache_key` | TEXT | SHA256(sorted stop IDs + criterion); used for Redis cache lookup |
| `status` | route_status ENUM | `planned`, `in_progress`, `completed`, `cancelled` |
| `created_at` | TIMESTAMPTZ | |

### deliveries
One delivery record per order within a route.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `order_id` | UUID FK → orders.id | UNIQUE; ON DELETE RESTRICT |
| `delivery_route_id` | UUID FK → delivery_routes.id | ON DELETE RESTRICT |
| `status` | delivery_status ENUM | `pending`, `picked_up`, `in_transit`, `delivered`, `failed` |
| `estimated_arrival_at` | TIMESTAMPTZ | From route calculation |
| `actual_arrival_at` | TIMESTAMPTZ | Set on delivery completion |
| `created_at` / `updated_at` | TIMESTAMPTZ | |

---

## Hub Module

### hubs
Distribution hub registry.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `name` | TEXT | |
| `address` / `latitude` / `longitude` | TEXT/NUMERIC | |
| `capacity_kg` | NUMERIC(10,2) | Maximum total weight |
| `is_active` | BOOLEAN | |
| `created_at` / `updated_at` | TIMESTAMPTZ | |
| `deleted_at` | TIMESTAMPTZ | |

### hub_inventory
Running balance per (hub, market_product).

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `hub_id` | UUID FK → hubs.id | |
| `market_product_id` | UUID FK → market_products.id | |
| `quantity_in` | NUMERIC(12,2) | Total received |
| `quantity_out` | NUMERIC(12,2) | Total dispatched |
| `quantity_available` | NUMERIC(12,2) | GENERATED ALWAYS AS (quantity_in - quantity_out) STORED |
| `updated_at` | TIMESTAMPTZ | |

**Constraint**: `UNIQUE (hub_id, market_product_id)`. Updated transactionally with inbound/outbound events.

### hub_inbound_events / hub_outbound_events
Append-heavy event records. Items stored as JSONB.

Key fields: `hub_id`, `source_market_id` (inbound) / `destination_route_id` (outbound),
`arrived_at` / `dispatched_at`, `items JSONB` (array of {market_product_id, quantity_kg}).

---

## Analytics Module

### analytics_aggregations
Pre-computed analytics rows for fast dashboard queries.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `type` | TEXT | `price_trend`, `demand_heatmap`, `delivery_performance` |
| `period_start` / `period_end` | TIMESTAMPTZ | Time window |
| `data_json` | JSONB | Pre-aggregated payload |
| `created_at` / `updated_at` | TIMESTAMPTZ | |

### export_jobs
Async CSV export job tracking.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `type` | TEXT | e.g., `price_history` |
| `status` | TEXT | `pending`, `processing`, `ready`, `failed` |
| `parameters` | JSONB | Request parameters |
| `file_path` | TEXT | Temp file path on server |
| `requested_at` | TIMESTAMPTZ | |
| `ready_at` | TIMESTAMPTZ | |
| `expires_at` | TIMESTAMPTZ | 24 hours after `ready_at` |

---

## Notifications Module

### notifications
Append-only notification log per user.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `user_id` | UUID FK → users.id | ON DELETE CASCADE |
| `type` | notification_type ENUM | `price_change`, `order_status`, `delivery_update`, `system` |
| `title` | TEXT | |
| `body` | TEXT | |
| `reference_id` | UUID | FK to the triggering entity (order_id, etc.) |
| `reference_type` | TEXT | e.g., `order`, `delivery` |
| `is_read` | BOOLEAN | DEFAULT false |
| `read_at` | TIMESTAMPTZ | |
| `created_at` | TIMESTAMPTZ | |

---

## Redis Key Patterns

| Key Pattern | Type | TTL | Content |
|-------------|------|-----|---------|
| `price:{marketId}:{productId}` | Hash | 5 min | Fields: `price`, `quantity` (HSET both atomically) |
| `reservation:{marketId}:{productId}` | String (counter) | 30 min | Integer: total soft-reserved quantity |
| `route:{sha256(stops+criterion)}` | String | 1 hour | JSON of RouteCalculationResponseDto |
| `analytics:{type}:{date}` | String | 15 min | JSON of pre-aggregated analytics payload |
| `analytics:price_trend:{mpId}:{from}:{to}` | String | 15 min | JSON of price trend time series |

---

## Domain Events → Integration Events

| Domain Event | Raised In | Published Integration Event | Consumers |
|---|---|---|---|
| `PriceUpdatedDomainEvent` | Pricing | `PriceUpdatedIntegrationEvent` (Contracts) | Notifications → SignalR broadcast |
| `OrderCreatedDomainEvent` | Orders | `OrderCreatedIntegrationEvent` | Logistics (creates delivery record), Notifications |
| `OrderStatusChangedDomainEvent` | Orders | `OrderStatusChangedIntegrationEvent` | Notifications → SignalR broadcast |
| `DeliveryStatusChangedDomainEvent` | Logistics | `DeliveryStatusChangedIntegrationEvent` | Notifications → SignalR broadcast |

---

## Entity Validation Rules

| Entity | Field | Rule |
|--------|-------|------|
| market_products | `current_price` | ≥ 0 (zero = "free") |
| market_products | `current_quantity` | ≥ 0 (zero = out of stock) |
| price_snapshots | `price` | ≥ 0 |
| orders | `status` transitions | Only allowed transitions (see state machine above) |
| order_items | `quantity` | > 0 integer |
| order_items | `unit_price` | > 0 NUMERIC |
| vehicles | `capacity_kg` | > 0 |
| scheduled_orders | `recurrence_type` | must be `daily` or `weekly` |
| refresh_tokens | `expires_at` | exactly 7 days from `created_at` |
