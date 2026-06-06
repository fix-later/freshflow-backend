# FreshFlow (FFX) — Database Schema

**Version:** 1.0  
**Date:** 2026-05-09  
**Project:** FreshFlow – Intermediary Platform for Food Procurement and Logistics Optimization  
**Status:** Approved for Implementation  
**Based on:** Requirements Specification v1.0 + System Architecture v1.0

---

## Table of Contents

1. [Entity Relationship Overview](#1-entity-relationship-overview)
2. [Table Definitions (DDL)](#2-table-definitions-ddl)
3. [Index Strategy](#3-index-strategy)
4. [PostgreSQL-Specific Features](#4-postgresql-specific-features)
5. [Migration Strategy](#5-migration-strategy)
6. [Redis Data Structures](#6-redis-data-structures)

---

## 1. Entity Relationship Overview

### 1.1 Entity Summary Table

| Entity | Aggregate Root | Owned By Module | Relationship Summary |
|--------|---------------|-----------------|----------------------|
| `roles` | No | Auth | Lookup table for global user roles. Seeded values: `admin`, `operations_manager`, `market_agent`, `hub_staff`, `driver`, `restaurant`. |
| `users` | Yes | Auth | Root of all user accounts. Each user has exactly one global role through `users.role_id`. Optional `phone` can be used as a second login identifier. One-to-one with `restaurants` (restaurant role) and `driver_profiles` (driver role). One-to-many with `refresh_tokens`, `notifications`. Referenced by nearly every table as actor/author FK. |
| `refresh_tokens` | No | Auth | Many-to-one with `users`. Append-only; each row is an issued token. |
| `user_market_assignments` | No | Auth | Many-to-many join between `users` (market_agent) and `markets`. Enforces market-level access control for Market Agents. |
| `markets` | Yes | Pricing | Root of all market-level data. One-to-many with `market_products`. |
| `products` | Yes | Pricing | System-wide product catalog. Many-to-many with `markets` via `market_products`. |
| `market_products` | No | Pricing | Join entity between `markets` and `products` carrying current price/quantity state. One-to-many with `price_snapshots`, `order_items`, `hub_inventory`. |
| `price_snapshots` | No | Pricing | Append-only audit log. Many-to-one with `market_products`. Partitioned by month. |
| `system_config` | No | Pricing | Key-value store for Admin-configurable parameters (e.g., cutoff time, price band tolerance). |
| `restaurants` | Yes | Orders | One-to-one with `users`. One-to-many with `orders`. |
| `orders` | Yes | Orders | Aggregate root of the order lifecycle. One-to-many with `order_items`. Many-to-one with `restaurants`, `order_groups`. |
| `order_items` | No | Orders | Line items. Many-to-one with `orders` and `market_products`. |
| `order_groups` | Yes | Orders | Groups multiple confirmed orders for batch logistics dispatch. One-to-many with `orders`. |
| `scheduled_orders` | Yes | Orders | Recurring order template. One-to-many with `orders` (instances). |
| `hubs` | Yes | Hub | Distribution hub registry. One-to-many with `hub_inventory`, `hub_inbound_events`, `hub_outbound_events`, `cross_dock_transfers`. |
| `hub_inventory` | No | Hub | Running stock balance per (hub, market_product). Updated transactionally on inbound/outbound events. |
| `hub_inbound_events` | No | Hub | Append-heavy inbound tracking records. Many-to-one with `hubs`. |
| `hub_outbound_events` | No | Hub | Append-heavy outbound tracking records. Many-to-one with `hubs`. |
| `cross_dock_transfers` | No | Hub | Records a direct transfer from inbound delivery to outbound route at a hub. |
| `vehicles` | Yes | Logistics | Vehicle fleet registry. One-to-many with `delivery_routes`. |
| `delivery_routes` | Yes | Logistics | Calculated route with ordered stop list in JSONB. One-to-many with `deliveries`. Many-to-one with `vehicles`. |
| `deliveries` | No | Logistics | One delivery stop per order within a route. One-to-one with `orders`. Many-to-one with `delivery_routes`. |
| `payments` | No | Payment | Per-order payment records. Many-to-one with `orders` (by ID, no FK). |
| `refunds` | No | Payment | Partial/full refund records. Many-to-one with `payments`. |
| `driver_profiles` | No | Auth/Logistics | Extended profile for `driver` role users. One-to-one with `users`. |
| `notifications` | No | Notifications | Append-only notification log. Many-to-one with `users`. |
| `analytics_aggregations` | No | Analytics | Pre-computed analytics rows. Read by Analytics module only. |
| `export_jobs` | No | Analytics | Async CSV export job tracking. |

### 1.2 Key Relationships

```
roles ──< users
users ──< refresh_tokens
users ──< user_market_assignments >── markets
users ──< notifications
users ──1 restaurants ──< orders ──< order_items >── market_products
                        orders >── order_groups
                        orders ──1 deliveries >── delivery_routes >── vehicles
markets ──< market_products ──< price_snapshots
market_products ──< hub_inventory >── hubs
hubs ──< hub_inbound_events
hubs ──< hub_outbound_events
hubs ──< cross_dock_transfers
users ──< scheduled_orders ──< orders (instances)
```

**Legend:** `──<` = one-to-many, `──1` = one-to-one, `>──` = many-to-one

### 1.3 DDD Aggregate Roots

Aggregate roots are entities through which all external access to their cluster must pass. Their IDs are safe to expose in URLs and API responses.

- **Auth:** `users` (and by extension its `refresh_tokens`)
- **Pricing:** `markets`, `products` (with `market_products` and `price_snapshots` as internal entities)
- **Orders:** `orders` (with `order_items` as internal), `order_groups`, `scheduled_orders`, `restaurants`
- **Logistics:** `delivery_routes` (with `deliveries` as internal), `vehicles`
- **Hub:** `hubs` (with `hub_inventory`, `hub_inbound_events`, `hub_outbound_events` as internal)
- **Analytics:** No aggregate roots — read-only cross-module queries

---

## 2. Table Definitions (DDL)

All tables use:
- UUID primary keys generated server-side via `gen_random_uuid()`
- `snake_case` naming throughout
- `created_at` / `updated_at` timestamps (except append-only tables noted inline)
- `deleted_at TIMESTAMPTZ` for soft delete (NULL = active), except append-only tables

```sql
-- ============================================================
-- ENUM TYPE DEFINITIONS
-- ============================================================

CREATE TYPE order_status AS ENUM (
    'draft',             -- order created, not yet confirmed
    'payment_pending',   -- payment initiated, awaiting gateway response
    'confirmed',         -- payment succeeded, price locked
    'batched',           -- included in a procurement batch after cutoff
    'picked_up',         -- Market Agent has purchased and departed market
    'at_hub',            -- goods arrived at distribution hub
    'delivering',        -- driver dispatched, en route to restaurant
    'delivered',         -- restaurant confirmed receipt
    'cancelled'
);

CREATE TYPE payment_status AS ENUM ('pending', 'paid', 'refunded', 'failed');

CREATE TYPE order_group_status AS ENUM ('open', 'locked', 'dispatched', 'completed');

CREATE TYPE delivery_status AS ENUM (
    'pending',
    'picked_up',
    'in_transit',
    'delivered',
    'failed'
);

CREATE TYPE route_status AS ENUM ('planned', 'in_progress', 'completed', 'cancelled');

CREATE TYPE notification_type AS ENUM (
    'price_change',
    'order_status',
    'delivery_update',
    'system'
);

-- ============================================================
-- AUTH MODULE TABLES
-- ============================================================

-- roles
-- Lookup table for global roles. Role names are stable lowercase strings exposed by the API.
CREATE TABLE roles (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(50)     NOT NULL,
    description     VARCHAR(255)    NOT NULL,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT roles_name_unique UNIQUE (name)
);

-- users
-- Aggregate root for all user accounts.
-- Soft delete: deleted_at IS NOT NULL means the account is deactivated.
-- is_active: used for fast active-status checks without reading deleted_at.
-- role_id: one global role per user through the roles lookup table.
-- phone: optional second login identifier. Phone OTP/SMS verification is deferred in v1.
CREATE TABLE users (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    email           VARCHAR(255)    NOT NULL,
    phone           VARCHAR(20),
    password_hash   TEXT            NOT NULL,
    role_id         UUID            NOT NULL,
    is_active       BOOLEAN         NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT users_email_unique UNIQUE (email),
    CONSTRAINT fk_users_role
        FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE RESTRICT
);

-- refresh_tokens
-- Append-only — no updated_at or deleted_at.
-- token_hash: stores bcrypt hash of the raw token, never the raw token itself.
-- family_id: groups tokens issued from the same login session for family-wide
--   invalidation on reuse detection (FR-AUTH-007).
-- replaced_by_token_id: links the chain of rotated tokens for audit trail.
-- revoked_at: NULL = still valid. Set to NOW() on use (rotation) or logout.
CREATE TABLE refresh_tokens (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 UUID        NOT NULL,
    token_hash              TEXT        NOT NULL,
    family_id               UUID        NOT NULL,
    expires_at              TIMESTAMPTZ NOT NULL,
    revoked_at              TIMESTAMPTZ,
    replaced_by_token_id    UUID,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT refresh_tokens_token_hash_unique UNIQUE (token_hash),
    CONSTRAINT fk_refresh_tokens_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT fk_refresh_tokens_replaced_by
        FOREIGN KEY (replaced_by_token_id) REFERENCES refresh_tokens (id) ON DELETE SET NULL
);

-- user_market_assignments
-- Maps market_agent users to their assigned markets.
-- Enforces server-side that a Market Agent can only update prices for their assigned market(s).
-- Design decision: an Agent may be assigned to multiple markets (e.g., covers both Hoc Mon and Binh Dien).
CREATE TABLE user_market_assignments (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID        NOT NULL,
    market_id   UUID        NOT NULL,
    assigned_by UUID,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT user_market_assignments_unique UNIQUE (user_id, market_id),
    CONSTRAINT fk_uma_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT fk_uma_market
        FOREIGN KEY (market_id) REFERENCES markets (id) ON DELETE CASCADE,
    CONSTRAINT fk_uma_assigned_by
        FOREIGN KEY (assigned_by) REFERENCES users (id) ON DELETE SET NULL
);

-- ============================================================
-- PRICING MODULE TABLES
-- ============================================================

-- markets
-- Wholesale market definitions. Three seeded markets for v1: Hoc Mon, Binh Dien, Thu Duc.
CREATE TABLE markets (
    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT            NOT NULL,
    location    TEXT,
    address     TEXT,
    latitude    NUMERIC(9, 6),
    longitude   NUMERIC(9, 6),
    is_active   BOOLEAN         NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at  TIMESTAMPTZ
);

-- products
-- System-wide product catalog. Only Admin can create/deactivate products (FR-PRI-006, GA-007).
-- Soft delete: deleted_at IS NOT NULL means the product is inactive (removed from active listings).
CREATE TABLE products (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT        NOT NULL,
    category    TEXT,
    unit        TEXT        NOT NULL,
    description TEXT,
    created_by  UUID,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at  TIMESTAMPTZ,
    CONSTRAINT fk_products_created_by
        FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE SET NULL
);

-- market_products
-- Join entity: tracks which products are available at which market, with current price and quantity.
-- current_price / current_quantity: live state managed by Market Agent.
-- reserved_quantity: tracks soft-reserved quantity (decremented from available for pending orders).
--   The application layer computes available = current_quantity - reserved_quantity.
-- updated_by: last Market Agent user who touched this row.
-- Design decision: no deleted_at here — deactivation is handled by soft-deleting the product
--   or deactivating the market. A market_products row is effectively deactivated when either
--   parent is soft-deleted/deactivated.
CREATE TABLE market_products (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    market_id           UUID            NOT NULL,
    product_id          UUID            NOT NULL,
    current_price       NUMERIC(12, 2)  NOT NULL CHECK (current_price >= 0),
    current_quantity    INTEGER         NOT NULL CHECK (current_quantity >= 0),
    reserved_quantity   INTEGER         NOT NULL DEFAULT 0 CHECK (reserved_quantity >= 0),
    updated_by          UUID,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT market_products_unique UNIQUE (market_id, product_id),
    CONSTRAINT fk_market_products_market
        FOREIGN KEY (market_id) REFERENCES markets (id) ON DELETE CASCADE,
    CONSTRAINT fk_market_products_product
        FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
    CONSTRAINT fk_market_products_updated_by
        FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE SET NULL
);

-- price_snapshots
-- Append-only, high-frequency write table. No updated_at or deleted_at.
-- Partitioned by RANGE on recorded_at (monthly). See Section 4 for partition DDL.
-- Design decision: no soft delete — price history is immutable by design (FR-PRI-004).
-- recorded_by: references users.id but ON DELETE SET NULL to preserve history even if user
--   account is removed. NULL means the recording user was deleted.
-- NOTE: The base table DDL uses PARTITION BY RANGE. Child partitions are defined in Section 4.
CREATE TABLE price_snapshots (
    id                  UUID            NOT NULL DEFAULT gen_random_uuid(),
    market_product_id   UUID            NOT NULL,
    price               NUMERIC(12, 2)  NOT NULL CHECK (price >= 0),
    quantity            INTEGER         NOT NULL CHECK (quantity >= 0),
    recorded_by         UUID,
    recorded_at         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_price_snapshots_market_product
        FOREIGN KEY (market_product_id) REFERENCES market_products (id) ON DELETE CASCADE,
    CONSTRAINT fk_price_snapshots_recorded_by
        FOREIGN KEY (recorded_by) REFERENCES users (id) ON DELETE SET NULL
) PARTITION BY RANGE (recorded_at);

-- system_config
-- Key-value store for Admin-configurable runtime parameters.
-- Example keys: 'daily_order_cutoff_time' = '22:00', 'price_band_tolerance_percent' = '10.00'
-- No soft delete — config rows are overwritten in place; audit is covered by updated_at + updated_by.
CREATE TABLE system_config (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    key         TEXT        NOT NULL,
    value       TEXT        NOT NULL,
    description TEXT,
    updated_by  UUID,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT system_config_key_unique UNIQUE (key),
    CONSTRAINT fk_system_config_updated_by
        FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE SET NULL
);

-- ============================================================
-- ORDERS MODULE TABLES
-- ============================================================

-- restaurants
-- One restaurant account per user (UNIQUE on user_id).
-- is_approved: false = PENDING_APPROVAL state (GA-009). Restaurant cannot place orders until approved.
CREATE TABLE restaurants (
    id          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID        NOT NULL,
    name        TEXT        NOT NULL,
    address     TEXT,
    latitude    NUMERIC(9, 6),
    longitude   NUMERIC(9, 6),
    phone       TEXT,
    is_approved BOOLEAN     NOT NULL DEFAULT false,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at  TIMESTAMPTZ,
    CONSTRAINT restaurants_user_id_unique UNIQUE (user_id),
    CONSTRAINT fk_restaurants_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

-- order_groups
-- Administrative grouping of confirmed orders for batch logistics dispatch.
-- total_orders: denormalized count, updated by the application when orders are added/removed.
-- Design decision: grouped_by references the admin user who created the group.
CREATE TABLE order_groups (
    id              UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    name            TEXT,
    status          order_group_status  NOT NULL DEFAULT 'open',
    grouped_by      UUID,
    total_orders    INTEGER             NOT NULL DEFAULT 0 CHECK (total_orders >= 0),
    created_at      TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT fk_order_groups_grouped_by
        FOREIGN KEY (grouped_by) REFERENCES users (id) ON DELETE SET NULL
);

-- orders
-- Aggregate root of the order lifecycle.
-- order_group_id: nullable — NULL means not yet batched.
-- scheduled_order_id: nullable — NULL means one-off order; set if generated from a scheduled_orders record.
-- payment_status: tracks payment lifecycle separately from order status.
-- cancelled_at: set when status transitions to 'cancelled'.
-- total_amount: maintained by application on order_item creation/update. Denormalized for fast reads.
-- Design decision: ON DELETE RESTRICT on restaurant_id to prevent accidental deletion of a restaurant
--   with active orders. Use soft delete on restaurant instead.
-- Design decision: order_group_id uses ON DELETE SET NULL — an order is ungrouped if its group is deleted.
CREATE TABLE orders (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    restaurant_id       UUID            NOT NULL,
    order_group_id      UUID,
    scheduled_order_id  UUID,
    status              order_status    NOT NULL DEFAULT 'draft',
    payment_status      payment_status  NOT NULL DEFAULT 'pending',
    scheduled_for       TIMESTAMPTZ,
    total_amount        NUMERIC(14, 2)  NOT NULL DEFAULT 0 CHECK (total_amount >= 0),
    notes               TEXT,
    cancelled_at        TIMESTAMPTZ,
    cancellation_reason TEXT,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT fk_orders_restaurant
        FOREIGN KEY (restaurant_id) REFERENCES restaurants (id) ON DELETE RESTRICT,
    CONSTRAINT fk_orders_order_group
        FOREIGN KEY (order_group_id) REFERENCES order_groups (id) ON DELETE SET NULL,
    CONSTRAINT fk_orders_scheduled_order
        FOREIGN KEY (scheduled_order_id) REFERENCES scheduled_orders (id) ON DELETE SET NULL
);

-- order_items
-- Line items for an order.
-- unit_price: price at order creation (DRAFT) time — may differ from locked_unit_price.
-- product_name_snapshot: product name at order creation time, preserved for history even if product is renamed.
-- locked_unit_price: set when order transitions to CONFIRMED (after payment). Immune to subsequent
--   market price changes. NULL until confirmed. This is what the restaurant actually pays.
-- locked_total: quantity × locked_unit_price. Set at CONFIRMED time.
-- actual_quantity: actual quantity delivered; may be less than quantity if Hub Staff flags shortage.
-- subtotal: GENERATED ALWAYS AS computed column based on quantity × unit_price (draft estimate).
-- Design decision: no deleted_at. If an item must be removed, the order itself is cancelled.
--   Individual item cancellation is not a v1 feature.
-- Design decision: ON DELETE RESTRICT on market_product_id to preserve order integrity.
CREATE TABLE order_items (
    id                      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id                UUID            NOT NULL,
    market_product_id       UUID            NOT NULL,
    product_name_snapshot   VARCHAR(200)    NOT NULL,
    quantity                INTEGER         NOT NULL CHECK (quantity > 0),
    unit_price              NUMERIC(12, 2)  NOT NULL CHECK (unit_price >= 0),
    subtotal                NUMERIC(14, 2)  GENERATED ALWAYS AS (quantity * unit_price) STORED,
    locked_unit_price       NUMERIC(12, 2),
    locked_total            NUMERIC(14, 2),
    actual_quantity         NUMERIC(10, 2),
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_order_items_order
        FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_order_items_market_product
        FOREIGN KEY (market_product_id) REFERENCES market_products (id) ON DELETE RESTRICT
);

-- scheduled_orders
-- Recurring order template (FR-ORD-002). Generates concrete order instances on each tick.
-- recurrence_type: 'daily' or 'weekly'. Enforced by CHECK constraint.
-- first_run_at: when the first instance should be generated (in Asia/Ho_Chi_Minh tz).
-- last_executed_at: updated by the background job after each successful instance generation.
--   Used to enforce idempotency — the job checks this before generating a new instance.
-- cancelled_at: set when the restaurant or admin cancels the recurring schedule.
CREATE TABLE scheduled_orders (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    restaurant_id       UUID        NOT NULL,
    recurrence_type     TEXT        NOT NULL CHECK (recurrence_type IN ('daily', 'weekly')),
    first_run_at        TIMESTAMPTZ NOT NULL,
    last_executed_at    TIMESTAMPTZ,
    cancelled_at        TIMESTAMPTZ,
    notes               TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT fk_scheduled_orders_restaurant
        FOREIGN KEY (restaurant_id) REFERENCES restaurants (id) ON DELETE RESTRICT
);

-- ============================================================
-- HUB MODULE TABLES
-- ============================================================

-- hubs
-- Distribution hub registry. Managed by Admin.
-- capacity_kg: maximum storage capacity.
-- managed_by: admin user responsible for this hub.
CREATE TABLE hubs (
    id          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT            NOT NULL,
    address     TEXT,
    latitude    NUMERIC(9, 6),
    longitude   NUMERIC(9, 6),
    capacity_kg NUMERIC(10, 2)  CHECK (capacity_kg > 0),
    is_active   BOOLEAN         NOT NULL DEFAULT true,
    managed_by  UUID,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at  TIMESTAMPTZ,
    CONSTRAINT fk_hubs_managed_by
        FOREIGN KEY (managed_by) REFERENCES users (id) ON DELETE SET NULL
);

-- hub_inventory
-- Running stock balance per (hub, market_product) pair.
-- Updated transactionally alongside inbound/outbound events — must never diverge.
-- quantity_available: GENERATED ALWAYS AS computed column, always consistent.
-- Design decision: quantity_in and quantity_out are cumulative counters, not current state.
--   This allows auditing total flow through a hub without joining event tables.
--   quantity_available = quantity_in - quantity_out represents current on-hand.
CREATE TABLE hub_inventory (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id              UUID        NOT NULL,
    market_product_id   UUID        NOT NULL,
    quantity_in         INTEGER     NOT NULL DEFAULT 0 CHECK (quantity_in >= 0),
    quantity_out        INTEGER     NOT NULL DEFAULT 0 CHECK (quantity_out >= 0),
    quantity_available  INTEGER     GENERATED ALWAYS AS (quantity_in - quantity_out) STORED,
    recorded_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT hub_inventory_unique UNIQUE (hub_id, market_product_id),
    CONSTRAINT hub_inventory_non_negative_available
        CHECK (quantity_in >= quantity_out),
    CONSTRAINT fk_hub_inventory_hub
        FOREIGN KEY (hub_id) REFERENCES hubs (id) ON DELETE CASCADE,
    CONSTRAINT fk_hub_inventory_market_product
        FOREIGN KEY (market_product_id) REFERENCES market_products (id) ON DELETE RESTRICT
);

-- hub_inbound_events
-- Append-heavy inbound tracking records (FR-HUB-003).
-- Items (product/quantity pairs) stored in JSONB for flexibility.
-- delivery_route_id: links to the delivery route that brought the goods in.
-- hub_staff_user_id: the Hub Staff employee who received and scanned the goods.
-- condition_status: overall condition of the inbound batch. OK means no discrepancies.
-- discrepancy_notes: free-text notes when condition_status != OK.
CREATE TABLE hub_inbound_events (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id              UUID        NOT NULL,
    source_market_id    UUID,
    delivery_route_id   UUID,
    items               JSONB       NOT NULL DEFAULT '[]',
    total_quantity_kg   NUMERIC(10, 2),
    arrived_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    recorded_by         UUID,
    hub_staff_user_id   UUID,
    condition_status    VARCHAR(20) NOT NULL DEFAULT 'OK'
                            CHECK (condition_status IN ('OK', 'DAMAGED', 'MISSING', 'PARTIAL')),
    discrepancy_notes   TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_hub_inbound_hub
        FOREIGN KEY (hub_id) REFERENCES hubs (id) ON DELETE RESTRICT,
    CONSTRAINT fk_hub_inbound_market
        FOREIGN KEY (source_market_id) REFERENCES markets (id) ON DELETE SET NULL,
    CONSTRAINT fk_hub_inbound_route
        FOREIGN KEY (delivery_route_id) REFERENCES delivery_routes (id) ON DELETE SET NULL,
    CONSTRAINT fk_hub_inbound_recorded_by
        FOREIGN KEY (recorded_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT fk_hub_inbound_hub_staff
        FOREIGN KEY (hub_staff_user_id) REFERENCES users (id) ON DELETE SET NULL
);

-- hub_outbound_events
-- Append-heavy outbound tracking records (FR-HUB-004).
-- Items (product/quantity pairs) stored in JSONB for flexibility.
CREATE TABLE hub_outbound_events (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id                  UUID        NOT NULL,
    destination_route_id    UUID,
    items                   JSONB       NOT NULL DEFAULT '[]',
    total_quantity_kg       NUMERIC(10, 2),
    dispatched_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    recorded_by             UUID,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_hub_outbound_hub
        FOREIGN KEY (hub_id) REFERENCES hubs (id) ON DELETE RESTRICT,
    CONSTRAINT fk_hub_outbound_route
        FOREIGN KEY (destination_route_id) REFERENCES delivery_routes (id) ON DELETE SET NULL,
    CONSTRAINT fk_hub_outbound_recorded_by
        FOREIGN KEY (recorded_by) REFERENCES users (id) ON DELETE SET NULL
);

-- cross_dock_transfers
-- Records a direct transfer from an inbound delivery to an outbound route (FR-HUB-002).
-- status: 'pending', 'in_progress', 'completed' — not a PG enum to allow simpler additions.
CREATE TABLE cross_dock_transfers (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    hub_id                  UUID        NOT NULL,
    inbound_event_id        UUID        NOT NULL,
    outbound_route_id       UUID        NOT NULL,
    status                  TEXT        NOT NULL DEFAULT 'pending'
                                CHECK (status IN ('pending', 'in_progress', 'completed')),
    notes                   TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at              TIMESTAMPTZ,
    CONSTRAINT fk_cross_dock_hub
        FOREIGN KEY (hub_id) REFERENCES hubs (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cross_dock_inbound
        FOREIGN KEY (inbound_event_id) REFERENCES hub_inbound_events (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cross_dock_route
        FOREIGN KEY (outbound_route_id) REFERENCES delivery_routes (id) ON DELETE RESTRICT
);

-- ============================================================
-- PAYMENT MODULE TABLES
-- ============================================================

-- payments
-- Tracks per-order payment transactions via external payment gateway.
-- order_id: no FK constraint — cross-context reference by ID only (DDD rule).
-- gateway_transaction_id: the gateway's reference ID for reconciliation.
-- gateway_response: full gateway callback payload stored for audit.
CREATE TABLE payments (
    id                      UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id                UUID            NOT NULL,
    restaurant_id           UUID            NOT NULL,
    amount                  NUMERIC(14, 2)  NOT NULL CHECK (amount > 0),
    payment_method          VARCHAR(20)     NOT NULL
                                CHECK (payment_method IN ('VNPAY', 'MOMO', 'ZALOPAY')),
    gateway_transaction_id  VARCHAR(255),
    status                  VARCHAR(20)     NOT NULL DEFAULT 'PENDING'
                                CHECK (status IN ('PENDING', 'SUCCEEDED', 'FAILED', 'CANCELLED')),
    gateway_response        JSONB,
    paid_at                 TIMESTAMPTZ,
    created_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_payments_order_id ON payments (order_id);
CREATE INDEX idx_payments_restaurant_id ON payments (restaurant_id);
CREATE INDEX idx_payments_status ON payments (status) WHERE status = 'PENDING';

-- refunds
-- Tracks partial or full refunds issued against a payment.
-- Triggered by: hub discrepancy flags, order cancellation after payment.
-- reason: 'hub_shortage', 'hub_damage', 'customer_cancel', 'system'
CREATE TABLE refunds (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id      UUID            NOT NULL,
    order_id        UUID            NOT NULL,
    reason          VARCHAR(255)    NOT NULL
                        CHECK (reason IN ('hub_shortage', 'hub_damage', 'customer_cancel', 'system')),
    amount          NUMERIC(14, 2)  NOT NULL CHECK (amount > 0),
    status          VARCHAR(20)     NOT NULL DEFAULT 'PENDING'
                        CHECK (status IN ('PENDING', 'COMPLETED', 'FAILED')),
    refunded_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_refunds_payment
        FOREIGN KEY (payment_id) REFERENCES payments (id) ON DELETE RESTRICT
);

CREATE INDEX idx_refunds_payment_id ON refunds (payment_id);
CREATE INDEX idx_refunds_order_id ON refunds (order_id);

-- driver_profiles
-- Extended profile for users with role 'driver'.
-- current_vehicle_id: the vehicle currently assigned to this driver (nullable).
-- status: operational status for dispatch planning.
CREATE TABLE driver_profiles (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID        UNIQUE NOT NULL,
    license_number      VARCHAR(50),
    current_vehicle_id  UUID,
    status              VARCHAR(20) NOT NULL DEFAULT 'AVAILABLE'
                            CHECK (status IN ('AVAILABLE', 'ON_DUTY', 'OFF_DUTY')),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_driver_profiles_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT fk_driver_profiles_vehicle
        FOREIGN KEY (current_vehicle_id) REFERENCES vehicles (id) ON DELETE SET NULL
);

-- ============================================================
-- LOGISTICS MODULE TABLES
-- ============================================================

-- vehicles
-- Vehicle fleet registry. Managed by Admin.
-- plate_number: unique physical identifier for the vehicle.
-- is_available: false when assigned to an active route; enforced by application.
CREATE TABLE vehicles (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    plate_number    TEXT            NOT NULL,
    capacity_kg     NUMERIC(10, 2)  NOT NULL CHECK (capacity_kg > 0),
    vehicle_type    TEXT            NOT NULL,
    is_available    BOOLEAN         NOT NULL DEFAULT true,
    registered_by   UUID,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT vehicles_plate_number_unique UNIQUE (plate_number),
    CONSTRAINT fk_vehicles_registered_by
        FOREIGN KEY (registered_by) REFERENCES users (id) ON DELETE SET NULL
);

-- delivery_routes
-- Calculated delivery route. Stores the ordered stop list in route_metadata JSONB.
-- route_metadata schema: see Section 4.2.
-- order_group_id: links the route to the order group it serves.
-- Design decision: route is stored with its full stop list in JSONB because
--   the stop list is always read and written as a unit, and a relational
--   route_stops join table would add query complexity without benefit for reads.
--   Complex stop-level queries (analytics) are handled in the Analytics module.
CREATE TABLE delivery_routes (
    id                          UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_id                  UUID,
    order_group_id              UUID,
    status                      route_status    NOT NULL DEFAULT 'planned',
    route_metadata              JSONB,
    total_distance_km           NUMERIC(8, 2)   CHECK (total_distance_km >= 0),
    estimated_duration_minutes  INTEGER         CHECK (estimated_duration_minutes >= 0),
    actual_start_at             TIMESTAMPTZ,
    actual_end_at               TIMESTAMPTZ,
    created_by                  UUID,
    created_at                  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    deleted_at                  TIMESTAMPTZ,
    CONSTRAINT fk_delivery_routes_vehicle
        FOREIGN KEY (vehicle_id) REFERENCES vehicles (id) ON DELETE RESTRICT,
    CONSTRAINT fk_delivery_routes_order_group
        FOREIGN KEY (order_group_id) REFERENCES order_groups (id) ON DELETE SET NULL,
    CONSTRAINT fk_delivery_routes_created_by
        FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE SET NULL
);

-- deliveries
-- One row per order stop within a delivery route.
-- sequence_number: 1-based stop order within the route.
-- UNIQUE on order_id enforces one delivery per order at all times.
-- Design decision: ON DELETE RESTRICT on both FKs — deliveries are critical operational
--   records and must not be cascade-deleted accidentally.
CREATE TABLE deliveries (
    id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    delivery_route_id   UUID            NOT NULL,
    order_id            UUID            NOT NULL,
    sequence_number     INTEGER         NOT NULL CHECK (sequence_number > 0),
    status              delivery_status NOT NULL DEFAULT 'pending',
    estimated_arrival   TIMESTAMPTZ,
    actual_arrival      TIMESTAMPTZ,
    failure_reason      TEXT,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT deliveries_order_id_unique UNIQUE (order_id),
    CONSTRAINT fk_deliveries_route
        FOREIGN KEY (delivery_route_id) REFERENCES delivery_routes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_deliveries_order
        FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT
);

-- ============================================================
-- NOTIFICATIONS MODULE TABLES
-- ============================================================

-- notifications
-- Append-only persistent notification log.
-- No updated_at or deleted_at — notifications are immutable once created.
-- read_at: set when the user reads/dismisses the notification.
-- payload: event-specific JSON data. Schema varies by type — see Section 4.2.
CREATE TABLE notifications (
    id          UUID                PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID                NOT NULL,
    type        notification_type   NOT NULL,
    title       TEXT                NOT NULL,
    body        TEXT                NOT NULL,
    payload     JSONB,
    is_read     BOOLEAN             NOT NULL DEFAULT false,
    read_at     TIMESTAMPTZ,
    created_at  TIMESTAMPTZ         NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_notifications_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

-- ============================================================
-- ANALYTICS MODULE TABLES
-- ============================================================

-- analytics_aggregations
-- Pre-computed aggregation rows written by the AnalyticsAggregationJob background job.
-- type: describes what kind of aggregation (e.g., 'price_trend_daily', 'demand_heatmap_hourly').
-- period_start / period_end: the time window covered by the aggregation.
-- data_json: serialized aggregation result (structure depends on type).
CREATE TABLE analytics_aggregations (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    type            TEXT        NOT NULL,
    period_start    TIMESTAMPTZ NOT NULL,
    period_end      TIMESTAMPTZ NOT NULL,
    data_json       JSONB       NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT analytics_aggregations_unique UNIQUE (type, period_start, period_end)
);

-- export_jobs
-- Tracks async CSV export jobs (FR-ANA-004).
-- status: 'pending', 'processing', 'ready', 'failed'.
-- file_path: local temp storage path; application purges after 24 hours.
-- expires_at: when the export file is no longer available for download.
CREATE TABLE export_jobs (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    requested_by    UUID,
    export_type     TEXT        NOT NULL,
    status          TEXT        NOT NULL DEFAULT 'pending'
                        CHECK (status IN ('pending', 'processing', 'ready', 'failed')),
    parameters      JSONB,
    file_path       TEXT,
    error_message   TEXT,
    ready_at        TIMESTAMPTZ,
    expires_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_export_jobs_requested_by
        FOREIGN KEY (requested_by) REFERENCES users (id) ON DELETE SET NULL
);
```

> **Note on forward references:** `orders` references `scheduled_orders`, and `hub_inbound_events` / `hub_outbound_events` reference `delivery_routes`. When running as a migration, EF Core handles the dependency ordering automatically. If running raw DDL, create tables in this order:
> `roles` → `users` → `markets` → `products` → `market_products` → `price_snapshots` → `system_config` → `restaurants` → `order_groups` → `scheduled_orders` → `orders` → `order_items` → `hubs` → `hub_inventory` → `vehicles` → `delivery_routes` → `deliveries` → `hub_inbound_events` → `hub_outbound_events` → `cross_dock_transfers` → `user_market_assignments` → `notifications` → `analytics_aggregations` → `export_jobs`

> **No triggers policy:** All business logic (soft-reservation decrement, total_amount recalculation, status machine validation, hub capacity enforcement) lives in the application layer (ASP.NET Core services). No PostgreSQL triggers are used. This decision keeps business rules in a single, testable, language-native location and avoids hidden side effects during migrations and testing.

---

## 3. Index Strategy

```sql
-- ============================================================
-- roles
-- ============================================================

-- Covered by UNIQUE constraint: roles_name_unique
-- Already creates an index on name — used by AdminSeeder and Admin user creation.

-- ============================================================
-- users
-- ============================================================

-- Covered by UNIQUE constraint: users_email_unique
-- Already creates an index on email — no separate CREATE INDEX needed.

-- Supports phone login and duplicate-phone checks while allowing users without phone.
CREATE UNIQUE INDEX idx_users_phone_not_null
    ON users (phone)
    WHERE phone IS NOT NULL;

-- Supports admin queries filtering by role and active status.
-- e.g. "list all active market agents"
CREATE INDEX idx_users_role_id_is_active
    ON users (role_id, is_active)
    WHERE deleted_at IS NULL;

-- Supports soft-delete scoping on any user lookup
CREATE INDEX idx_users_deleted_at
    ON users (deleted_at)
    WHERE deleted_at IS NOT NULL;

-- ============================================================
-- refresh_tokens
-- ============================================================

-- Covered by UNIQUE constraint: refresh_tokens_token_hash_unique
-- Already creates an index on token_hash — used on every /auth/refresh lookup.

-- Supports querying all valid (non-revoked) tokens for a user.
-- e.g. "list active sessions for user" or "count active tokens" (GA-010 / FR-AUTH-002)
CREATE INDEX idx_refresh_tokens_user_id_revoked_at
    ON refresh_tokens (user_id, revoked_at);

-- Supports token family invalidation queries.
-- e.g. "revoke all tokens in family X"
CREATE INDEX idx_refresh_tokens_family_id
    ON refresh_tokens (family_id);

-- Supports cleanup job finding expired tokens to purge
CREATE INDEX idx_refresh_tokens_expires_at
    ON refresh_tokens (expires_at);

-- ============================================================
-- user_market_assignments
-- ============================================================

-- Covered by UNIQUE constraint: user_market_assignments_unique (user_id, market_id)

-- Supports "which markets is this user assigned to?" (kiosk staff authorization check)
CREATE INDEX idx_uma_user_id
    ON user_market_assignments (user_id);

-- Supports "which staff are assigned to this market?" (admin view)
CREATE INDEX idx_uma_market_id
    ON user_market_assignments (market_id);

-- ============================================================
-- markets
-- ============================================================

-- Supports active market listings (most common query)
CREATE INDEX idx_markets_is_active
    ON markets (is_active)
    WHERE deleted_at IS NULL;

-- ============================================================
-- products
-- ============================================================

-- Supports active product catalog listings
CREATE INDEX idx_products_deleted_at
    ON products (deleted_at)
    WHERE deleted_at IS NULL;

-- Supports filtering by category (e.g., "show all seafood products")
CREATE INDEX idx_products_category
    ON products (category)
    WHERE deleted_at IS NULL;

-- ============================================================
-- market_products
-- ============================================================

-- Supports "get all products for a market" (primary product listing query)
CREATE INDEX idx_market_products_market_id
    ON market_products (market_id);

-- Supports "get all markets carrying this product" (cross-market price comparison)
CREATE INDEX idx_market_products_product_id
    ON market_products (product_id);

-- ============================================================
-- price_snapshots
-- ============================================================
-- CRITICAL INDEX — most frequent analytical read pattern.
-- "Get the most recent N price snapshots for a market_product."
-- Must be created on each monthly partition OR as a partitioned index on the parent table.
-- Using CREATE INDEX on the parent table automatically covers all partitions in PG 11+.
CREATE INDEX idx_price_snapshots_market_product_recorded_at
    ON price_snapshots (market_product_id, recorded_at DESC);

-- Supports time-range queries for price trend analytics (FR-ANA-001)
CREATE INDEX idx_price_snapshots_recorded_at
    ON price_snapshots (recorded_at DESC);

-- Supports audit queries "what did user X record?"
CREATE INDEX idx_price_snapshots_recorded_by
    ON price_snapshots (recorded_by);

-- ============================================================
-- restaurants
-- ============================================================

-- Covered by UNIQUE constraint: restaurants_user_id_unique

-- Supports admin listing: approved vs. pending restaurants
CREATE INDEX idx_restaurants_is_approved
    ON restaurants (is_approved)
    WHERE deleted_at IS NULL;

-- ============================================================
-- order_groups
-- ============================================================

-- Supports filtering groups by status (e.g., "show all open groups")
CREATE INDEX idx_order_groups_status
    ON order_groups (status)
    WHERE deleted_at IS NULL;

-- ============================================================
-- orders
-- ============================================================

-- Primary pattern: restaurant viewing their own orders by status
CREATE INDEX idx_orders_restaurant_id_status
    ON orders (restaurant_id, status)
    WHERE deleted_at IS NULL;

-- Supports logistics grouping view: "which orders are in group X?"
CREATE INDEX idx_orders_order_group_id
    ON orders (order_group_id)
    WHERE order_group_id IS NOT NULL;

-- Partial index: supports background job finding scheduled orders due for dispatch
CREATE INDEX idx_orders_scheduled_for
    ON orders (scheduled_for)
    WHERE scheduled_for IS NOT NULL AND deleted_at IS NULL;

-- Supports finding all instances of a recurring order
CREATE INDEX idx_orders_scheduled_order_id
    ON orders (scheduled_order_id)
    WHERE scheduled_order_id IS NOT NULL;

-- Supports status-based operational queries (e.g., "all pending orders across all restaurants")
CREATE INDEX idx_orders_status
    ON orders (status)
    WHERE deleted_at IS NULL;

-- ============================================================
-- order_items
-- ============================================================

-- Primary pattern: fetch all items for an order (always accessed via parent order)
CREATE INDEX idx_order_items_order_id
    ON order_items (order_id);

-- Supports "how many of product X has been ordered?" (analytics + stock planning)
CREATE INDEX idx_order_items_market_product_id
    ON order_items (market_product_id);

-- ============================================================
-- scheduled_orders
-- ============================================================

-- Supports background job finding active (non-cancelled) scheduled orders for tick processing
CREATE INDEX idx_scheduled_orders_restaurant_id
    ON scheduled_orders (restaurant_id)
    WHERE deleted_at IS NULL AND cancelled_at IS NULL;

-- ============================================================
-- hubs
-- ============================================================

-- Supports active hub listings for route calculation
CREATE INDEX idx_hubs_is_active
    ON hubs (is_active)
    WHERE deleted_at IS NULL;

-- ============================================================
-- hub_inventory
-- ============================================================

-- Covered by UNIQUE constraint: hub_inventory_unique (hub_id, market_product_id)
-- Supports "what stock does this hub have for product X?" (primary lookup)
CREATE INDEX idx_hub_inventory_hub_id
    ON hub_inventory (hub_id);

-- ============================================================
-- hub_inbound_events
-- ============================================================

-- Primary pattern: "show all inbound events for hub X on date Y" (FR-HUB-003)
CREATE INDEX idx_hub_inbound_events_hub_id_arrived_at
    ON hub_inbound_events (hub_id, arrived_at DESC);

-- ============================================================
-- hub_outbound_events
-- ============================================================

-- Primary pattern: "show all outbound events for hub X on date Y" (FR-HUB-004)
CREATE INDEX idx_hub_outbound_events_hub_id_dispatched_at
    ON hub_outbound_events (hub_id, dispatched_at DESC);

-- ============================================================
-- vehicles
-- ============================================================

-- Covered by UNIQUE constraint: vehicles_plate_number_unique

-- Supports listing available vehicles for route assignment
CREATE INDEX idx_vehicles_is_available
    ON vehicles (is_available)
    WHERE deleted_at IS NULL;

-- ============================================================
-- delivery_routes
-- ============================================================

-- Supports "all routes for vehicle X" (vehicle utilization view)
CREATE INDEX idx_delivery_routes_vehicle_id
    ON delivery_routes (vehicle_id);

-- Supports "get the route for order group X"
CREATE INDEX idx_delivery_routes_order_group_id
    ON delivery_routes (order_group_id)
    WHERE order_group_id IS NOT NULL;

-- Supports operational filtering by route status
CREATE INDEX idx_delivery_routes_status
    ON delivery_routes (status)
    WHERE deleted_at IS NULL;

-- ============================================================
-- deliveries
-- ============================================================

-- Covered by UNIQUE constraint: deliveries_order_id_unique

-- Primary pattern: "get all deliveries on route X in sequence order"
CREATE INDEX idx_deliveries_delivery_route_id_sequence
    ON deliveries (delivery_route_id, sequence_number);

-- Supports operational filtering: "all failed deliveries today"
CREATE INDEX idx_deliveries_status
    ON deliveries (status);

-- ============================================================
-- notifications
-- ============================================================

-- Primary pattern: "get unread notifications for user X, newest first"
-- This is a hot read path — every page load for a logged-in user.
CREATE INDEX idx_notifications_user_id_is_read_created_at
    ON notifications (user_id, is_read, created_at DESC);

-- Supports notification type filtering (e.g., "show only price_change alerts")
CREATE INDEX idx_notifications_user_id_type
    ON notifications (user_id, type);

-- ============================================================
-- analytics_aggregations
-- ============================================================

-- Covered by UNIQUE constraint: analytics_aggregations_unique (type, period_start, period_end)

-- Supports time-range scans within a given type
CREATE INDEX idx_analytics_aggregations_type_period
    ON analytics_aggregations (type, period_start, period_end);

-- ============================================================
-- export_jobs
-- ============================================================

-- Supports "get all export jobs for user X" (admin export history)
CREATE INDEX idx_export_jobs_requested_by_status
    ON export_jobs (requested_by, status);

-- Supports cleanup job finding expired export files
CREATE INDEX idx_export_jobs_expires_at
    ON export_jobs (expires_at)
    WHERE status = 'ready';
```

---

## 4. PostgreSQL-Specific Features

### 4.1 Table Partitioning — `price_snapshots`

`price_snapshots` is the highest-write-frequency table in FreshFlow (target: 100 updates/second across all markets, per NFR-2.1). Monthly RANGE partitioning on `recorded_at` provides:

1. **Partition pruning:** Queries scoped to a date range (e.g., a 30-day price trend) touch only the relevant monthly partitions, not the full table.
2. **Fast archival:** Old months can be detached (`DETACH PARTITION`) and archived or dropped without touching active data.
3. **Parallel writes:** PostgreSQL can perform parallel inserts across partitions in multi-session scenarios.

**Design decisions:**
- The primary key is defined on each partition individually (not on the parent), because PostgreSQL requires the partition key column (`recorded_at`) to be part of any unique constraint on a partitioned table. Since `id` alone is the logical primary key, uniqueness is enforced per partition.
- The default partition (`price_snapshots_default`) captures any data that falls outside the defined monthly partitions — for example, if a partition for the next month has not yet been created. The background job creates next-month's partition on the 25th of the current month.
- Indexes defined on the parent table (Section 3) automatically propagate to all partitions in PostgreSQL 11+.

```sql
-- Base partitioned table (defined in Section 2, repeated here for context)
-- CREATE TABLE price_snapshots (...) PARTITION BY RANGE (recorded_at);

-- Default partition: captures overflow rows when no monthly partition matches.
-- Prevents insert failures if a partition is not yet created.
CREATE TABLE price_snapshots_default
    PARTITION OF price_snapshots DEFAULT;

-- Sample monthly partition: January 2026
CREATE TABLE price_snapshots_2026_01
    PARTITION OF price_snapshots
    FOR VALUES FROM ('2026-01-01 00:00:00+00') TO ('2026-02-01 00:00:00+00');

-- Sample monthly partition: February 2026
CREATE TABLE price_snapshots_2026_02
    PARTITION OF price_snapshots
    FOR VALUES FROM ('2026-02-01 00:00:00+00') TO ('2026-03-01 00:00:00+00');

-- Sample monthly partition: May 2026 (current month at schema creation time)
CREATE TABLE price_snapshots_2026_05
    PARTITION OF price_snapshots
    FOR VALUES FROM ('2026-05-01 00:00:00+00') TO ('2026-06-01 00:00:00+00');

-- Sample monthly partition: June 2026 (created by background job on May 25)
CREATE TABLE price_snapshots_2026_06
    PARTITION OF price_snapshots
    FOR VALUES FROM ('2026-06-01 00:00:00+00') TO ('2026-07-01 00:00:00+00');
```

**Partition creation automation:** The `AnalyticsAggregationJob` (or a dedicated `PartitionMaintenanceJob`) checks on the 25th of each month whether the next month's partition exists. If it does not, it issues the `CREATE TABLE ... PARTITION OF` DDL. This prevents the default partition from accumulating data and keeps the index cardinality per partition manageable.

**Monitoring recommendation:** Add a health alert if `price_snapshots_default` accumulates more than 1,000 rows — this indicates the partition creation job has failed.

---

### 4.2 JSONB Columns

#### `delivery_routes.route_metadata`

Stores the full ordered stop list as computed by the VRP solver. The route is always read and written as a unit, making JSONB appropriate over a normalized `route_stops` join table. Analytics on stop-level data reads this JSONB via `jsonb_array_elements`.

**Schema:**
```json
{
  "stops": [
    {
      "sequence": 1,
      "type": "market",
      "entity_id": "uuid-of-market",
      "entity_name": "Chợ đầu mối Hóc Môn",
      "lat": 10.8912,
      "lng": 106.5981,
      "estimated_arrival": "2026-05-10T02:00:00+07:00",
      "estimated_departure": "2026-05-10T02:30:00+07:00"
    },
    {
      "sequence": 2,
      "type": "hub",
      "entity_id": "uuid-of-hub",
      "entity_name": "FFX Hub Tân Bình",
      "lat": 10.8014,
      "lng": 106.6520,
      "estimated_arrival": "2026-05-10T03:15:00+07:00",
      "estimated_departure": "2026-05-10T03:30:00+07:00"
    },
    {
      "sequence": 3,
      "type": "restaurant",
      "entity_id": "uuid-of-restaurant",
      "entity_name": "Nhà hàng Phở Bà Tư",
      "lat": 10.7769,
      "lng": 106.7009,
      "estimated_arrival": "2026-05-10T04:00:00+07:00",
      "estimated_departure": null
    }
  ],
  "optimization_criterion": "cost",
  "route_type": "hub_relay",
  "estimated_cost_vnd": 450000,
  "total_distance_km": 42.7,
  "estimated_duration_minutes": 120
}
```

**GIN index for JSONB querying** (optional — add if stop-level filtering becomes a common query):
```sql
-- Only add if Analytics module queries stop entities by entity_id within route_metadata
CREATE INDEX idx_delivery_routes_route_metadata_gin
    ON delivery_routes USING GIN (route_metadata jsonb_path_ops);
```

#### `notifications.payload`

Stores event-specific data alongside the human-readable `title` and `body`. Structure varies by `notification_type`.

**Schema by notification_type:**

```json
// type: 'price_change'
{
  "market_id": "uuid",
  "product_id": "uuid",
  "market_product_id": "uuid",
  "previous_price": "125000.00",
  "new_price": "135000.00",
  "new_quantity": "420",
  "updated_at": "2026-05-10T04:10:00+07:00"
}

// type: 'order_status'
{
  "order_id": "uuid",
  "previous_status": "confirmed",
  "new_status": "in_transit",
  "changed_by_user_id": "uuid",
  "estimated_delivery_at": "2026-05-10T06:00:00+07:00"
}

// type: 'delivery_update'
{
  "delivery_route_id": "uuid",
  "order_id": "uuid",
  "delivery_status": "delivered",
  "actual_arrival": "2026-05-10T05:47:00+07:00",
  "estimated_arrival": "2026-05-10T06:00:00+07:00"
}

// type: 'system'
{
  "event_code": "MAINTENANCE_WINDOW",
  "message_vi": "Hệ thống sẽ bảo trì từ 01:00–03:00 ngày mai.",
  "message_en": "System maintenance scheduled 01:00–03:00 tomorrow."
}
```

#### `hub_inbound_events.items` and `hub_outbound_events.items`

```json
[
  {
    "market_product_id": "uuid",
    "product_name": "Cá lóc",
    "quantity_kg": 150.5
  },
  {
    "market_product_id": "uuid",
    "product_name": "Tôm sú",
    "quantity_kg": 80.0
  }
]
```

---

### 4.3 Enum Types

Auth roles are stored in the `roles` lookup table, not as a PostgreSQL enum. The remaining PostgreSQL enum types are defined at the top of the DDL block in Section 2. Summary:

| Enum Type | Values | Used In |
|-----------|--------|---------|
| `order_status` | `draft`, `payment_pending`, `confirmed`, `batched`, `picked_up`, `at_hub`, `delivering`, `delivered`, `cancelled` | `orders.status` |
| `payment_status` | `pending`, `paid`, `refunded`, `failed` | `orders.payment_status` |
| `order_group_status` | `open`, `locked`, `dispatched`, `completed` | `order_groups.status` |
| `delivery_status` | `pending`, `picked_up`, `in_transit`, `delivered`, `failed` | `deliveries.status` |
| `route_status` | `planned`, `in_progress`, `completed`, `cancelled` | `delivery_routes.status` |
| `notification_type` | `price_change`, `order_status`, `delivery_update`, `system` | `notifications.type` |

**Trade-off:** PostgreSQL ENUMs are efficient (stored as integers internally) but require a DDL migration to add new values (`ALTER TYPE ... ADD VALUE`). For `cross_dock_transfers.status` and `export_jobs.status` (which may evolve), a `TEXT` column with a `CHECK` constraint is used instead — simpler to extend in application code without a blocking migration.

---

### 4.4 No Triggers Policy

No PostgreSQL triggers are used in this schema. All business logic resides in the ASP.NET Core application layer:

- `order_items.subtotal` — computed column (GENERATED ALWAYS AS), not a trigger
- `hub_inventory.quantity_available` — computed column (GENERATED ALWAYS AS), not a trigger
- `orders.total_amount` — maintained by `OrderService` within the EF Core transaction
- Hub capacity enforcement — handled in `HubService.RecordInboundAsync()` and `HubService.RecordOutboundAsync()`
- Soft-reservation management — handled by `SoftReservationService` with Redis + PostgreSQL coordination

**Rationale:** Triggers create hidden side effects that are difficult to unit test, debug, and reason about. All observable state changes must be traceable through the application's service layer, which is covered by CI unit tests (minimum 70% coverage per NFR-2.6).

---

## 5. Migration Strategy

### 5.1 Tool and Configuration

- **ORM:** EF Core 10 with Npgsql provider for PostgreSQL
- **Migration runner:** `dbContext.Database.MigrateAsync()` called at application startup in `Program.cs` before `app.Run()` — migrations run automatically on deploy, no manual SQL scripts in production
- **Migration location:** `src/FreshFlow.Api/Migrations/`
- **EF Core model configuration:** Each module defines its entity configurations in `IEntityTypeConfiguration<T>` classes within its own folder (e.g., `Modules/Pricing/Data/PriceSnapshotConfiguration.cs`)

### 5.2 Naming Convention

```
{YYYYMMDD}_{HHMMSS}_{PascalCaseDescription}
```

Examples:
```
20260101_000001_InitialSchema
20260101_000002_SeedMarketsAndProducts
20260101_000003_AddPriceSnapshotsPartitioning
20260115_093000_AddScheduledOrders
20260201_120000_AddHubCrossDocking
```

### 5.3 Dev Seed Data

The following seed data is applied by a dedicated idempotent seeder run after migrations (checks existence before inserting):

| Entity | Count | Details |
|--------|-------|---------|
| `roles` | 6 | Canonical role names: `admin`, `operations_manager`, `market_agent`, `hub_staff`, `driver`, `restaurant` |
| `users` | 1 | Admin account: email/password from `AdminSeed:Email` / `AdminSeed:Password` config or `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` env vars; password is bcrypt-hashed |
| `markets` | 3 | Hoc Mon (10.8912° N, 106.5981° E), Binh Dien (10.7230° N, 106.6050° E), Thu Duc (10.8567° N, 106.7547° E) |
| `products` | 10 | Sample catalog covering categories: rau củ (vegetables), thủy hải sản (seafood), thịt (meat), gia vị (spices). Units: kg, bunch, piece |
| `market_products` | 10–15 | Associates sample products with markets, with initial price/quantity values |
| `hubs` | 2 | Hub Tân Bình (central HCM), Hub Bình Thạnh (northeast HCM) |
| `vehicles` | 3 | 1 truck (5,000 kg), 1 van (2,000 kg), 1 motorbike (50 kg) |
| `system_config` | 2 | `daily_order_cutoff_time` = `22:00`; `price_band_tolerance_percent` = `10.00` |

### 5.4 Production Migration Policy

1. Migrations are **backward-compatible**: no column renames or type changes in the same release as the code that uses them. Two-phase approach: add column (deploy) → remove old column (next release).
2. **Never run manual SQL in production.** All schema changes go through EF Core migrations.
3. **Rollback**: revert the migration class and re-deploy. EF Core's `Down()` method is implemented for every migration. Emergency rollback: `dotnet ef database update {PreviousMigrationName}` run against the production database with controlled traffic drain.
4. **Partition creation**: monthly `price_snapshots` partitions are created by the `PartitionMaintenanceJob` on the 25th of each month for the following month. This is a DDL statement issued from the application — EF Core migrations do not manage partition children dynamically.
5. **Zero-downtime migrations**: Nginx continues routing traffic during migration. The API container starts, runs `MigrateAsync()`, then begins accepting traffic. The rolling update strategy (Section 7.4 of architecture doc) ensures the old container is only stopped after the new one is healthy.

---

## 6. Redis Data Structures

All Redis keys follow the patterns below. The application uses StackExchange.Redis. TTLs are sliding unless marked "absolute."

| Key Pattern | Data Structure | Content | TTL | Invalidation Trigger | Notes |
|-------------|---------------|---------|-----|---------------------|-------|
| `price:{marketId}:{productId}` | Hash | Fields: `price` (string decimal e.g. `"125000.00"`), `quantity` (int string e.g. `"300"`), `reserved` (int string), `updated_at` (ISO 8601), `updated_by` (userId string) | 5 min (absolute, reset on each write) | Market Agent `PATCH /price` or `PATCH /quantity` — `PricingCacheWriter` calls `HSET` + `EXPIRE` atomically after PostgreSQL commit | On cache miss, fall back to `market_products` PostgreSQL table. Redis failure must NOT block the price write path — log and continue. |
| `session:{userId}:refresh_count` | String (integer) | Count of currently non-revoked refresh tokens for this user. Used to detect excessive concurrent sessions (future rate limiting). | 7 days absolute (matches refresh token TTL) | Incremented (`INCR`) on token issuance; decremented (`DECR`) on revocation or logout. Reset to 0 on full family invalidation. | Supplementary to the PostgreSQL `refresh_tokens` table. If Redis is unavailable, session counting is skipped gracefully — it is advisory, not a security control. |
| `route:{sha256(sorted_stop_ids + criterion)}` | String (JSON) | Serialized `DeliveryRoute` calculation result including stops, distances, durations, estimated costs. Same structure as `delivery_routes.route_metadata` JSONB. | 1 hour absolute | Explicit `DEL` on vehicle re-assignment to a route (`DeliveryScheduleService`); natural key change when stop list changes (hash changes → new key, old key expires) | Hash input: sorted concatenation of all stop entity UUIDs + optimization criterion string. Different criteria produce different cache keys. Prevents recomputing identical VRP requests within the hour window. |
| `analytics:price_trend:{marketProductId}:{date}` | String (JSON) | Pre-aggregated hourly price data for the given day. Array of `{hour, avg_price, min_price, max_price, snapshot_count}`. | 15 min absolute | Explicit `DEL` triggered by `PricingCacheWriter` on any new `price_snapshots` insert for the given `market_product_id` on that date | Date format: `YYYY-MM-DD` in `Asia/Ho_Chi_Minh` timezone. Cache miss falls back to `analytics_aggregations` table, then to live query on `price_snapshots` read replica. |
| `order:reservation:{marketProductId}` | String (integer) | Soft-reserved quantity for pending orders. Represents the total quantity across all active reservations for this market_product. | 30 min sliding (extended on each reservation) | Decremented (`DECRBY`) on order cancellation, order expiry (background job), or order confirmation (hard-deducted from PostgreSQL at this point). Reset to 0 on full release. | Separate from the price/quantity hash. Available quantity = `price:{marketId}:{productId}` field `quantity` minus this counter. If key is missing, assume 0 reserved. Application reconciles Redis and PostgreSQL every 5 minutes via `ReservationExpiryJob`. |
| `signalr:backplane` (internal) | Managed by StackExchange.Redis SignalR backplane | SignalR group membership and message routing between API instances. Channel names: `signalr:pricing:{marketId}`, `signalr:orders:{restaurantId}`, `signalr:delivery:{restaurantId}` | Session lifetime (connection lifetime) | Automatically cleared by SignalR backplane on client disconnect. `Groups.RemoveFromGroupAsync` on explicit leave. | Not accessed directly by application code. Managed entirely by `AddSignalR().AddStackExchangeRedis(...)`. Listed here for operational awareness — Redis key space monitoring will show these channels. |
| `rate_limit:{userId}:{endpoint}` | String (integer counter) | Rolling window request count for the given user on the given endpoint slug. Endpoint slugs: `auth_login`, `auth_refresh`, `price_update`, `order_create`, `analytics_export` | TTL = window size (60 seconds absolute, auto-expires) | Auto-expires after window closes. Counter is set with `INCR` + `EXPIRE` (only set EXPIRE on first increment to avoid sliding). | Per-user rate limiting. IP-based rate limiting for unauthenticated endpoints (`auth_login`, `auth_refresh`) uses `rate_limit:ip:{hashedIp}:{endpoint}` as key. Limits per NFR-2.3: auth 10/min, price update 120/min, order create 30/min, analytics export 5/min. |

### 6.1 Redis Key Expiry and Eviction Policy

- **Eviction policy:** `allkeys-lru` — Redis evicts least-recently-used keys when memory is full. This is safe because all Redis data is either cache (reconstructable from PostgreSQL) or soft state (reservations reconciled by background job).
- **Persistence:** AOF (`appendonly yes`, `appendfsync everysec`) — provides ~1-second durability window for soft-reservation data. Acceptable loss window given the 5-minute reconciliation job.
- **Redis failure handling:** Pricing cache failures are logged at `Warning` level and do not fail the request. Soft-reservation Redis failures are logged at `Error` level, the PostgreSQL write is rolled back, and HTTP 500 is returned (per NFR-2.4: Redis write failure rolls back PG write to maintain consistency).

### 6.2 Redis Key Space Summary

```
price:*                          → price/quantity cache (Hash), 5min TTL
session:*:refresh_count          → session counter (String), 7d TTL
route:*                          → computed route cache (String/JSON), 1h TTL
analytics:price_trend:*          → price trend cache (String/JSON), 15min TTL
order:reservation:*              → soft reservation counter (String), 30min sliding TTL
rate_limit:*                     → rate limit counter (String), 60s TTL
(signalr internal keys)          → managed by StackExchange.Redis SignalR backplane
```

---

*End of FreshFlow Database Schema Document v1.0*

*Prepared by: Database Design Agent | Project: FFX Capstone 2026*
