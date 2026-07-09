# FreshFlow (FFX) — Database Schema

**Version:** 1.1  
**Date:** 2026-05-09 (design) · **Reconciled with code:** 2026-06-29  
**Project:** FreshFlow – Intermediary Platform for Food Procurement and Logistics Optimization  
**Status:** Partially implemented — see Sync Status below  
**Based on:** Requirements Specification v1.0 + System Architecture v1.0

---

## ⚙️ Sync Status (reviewed 2026-06-30)

> **Physical source of truth:** EF Core migrations plus the model snapshot at
> `src/FreshFlow.Infrastructure.Persistence/Migrations/AppDbContextModelSnapshot.cs`.
> The companion ER diagram `docs/database/03-database-schema.dbml` is a logical ERD for the
> implemented tables and enforced DB relationships where present. It is not a physical
> migration script because the current EF schema uses mixed PascalCase/snake_case column
> names unless a configuration explicitly maps a column.

**Implemented today** (Auth, Catalog, Pricing, Orders, Assistant, Notifications):
`roles`, `users`, `refresh_tokens`, `password_reset_tokens`, `verification_codes`,
`user_market_assignments`, `driver_profiles`, `restaurants`, `delivery_addresses`,
`product_categories`, `units_of_measurement`, `products`, `markets`, `market_products`,
`price_snapshots`, `orders`, `order_items`, `scheduled_orders`, `order_issues`,
`restaurant_credit`, `credit_transactions`, `assistant_conversations`,
`notifications`, `notification_devices` (epic NOT SCRUM-300).

**Planned — NOT yet in the database** (kept below for roadmap, marked `[PLANNED]`):
`system_config`, `order_groups`, `order_status_history`, `restaurant_members`,
`price_alert_subscriptions`, `invoices`, `invoice_orders`, `payments`, `refunds`,
`procurement_*`, `hubs`, `hub_*`, `cross_dock_transfers`, `vehicles`, `delivery_routes`,
`route_stops`, `deliveries`, `notification_templates`,
`analytics_aggregations`, `export_jobs`.

**Key deltas vs the original design (reflected in the implementation notes below):**
- IDs and `CreatedAt`/`UpdatedAt` are mostly assigned by the application/domain model
  (`BaseEntity`) rather than PostgreSQL `gen_random_uuid()` / `now()` defaults. Only
  defaults explicitly configured in EF/migrations should be treated as DB-level defaults.
- The SQL blocks below use logical `snake_case` names for readability. For exact physical
  column names and generated defaults, use the migrations/model snapshot.
- Status columns on implemented tables are stored as **`varchar`** via EF string conversion,
  not native PostgreSQL `ENUM` types. Values are stored **verbatim** (PascalCase, e.g. `Draft`,
  `Confirmed`, `Outstanding`) except `restaurants.status`, `order_issues.*`, and
  `credit_transactions.type` which EF stores **lowercase**.
- Inline `CHECK (...)` clauses shown in the DDL below are **application/domain-enforced
  invariants** — EF does not emit them, so they are **not** DB-level constraints (no migration
  creates a CHECK). Treat them as documentation of business rules.
- Cross-module FKs (`credit_transactions`/`restaurant_credit` → `restaurants`,
  `order_issues.reported_by` → `users`) are all **`ON DELETE RESTRICT`**. `restaurants.user_id`
  and `driver_profiles.user_id` are **logical 1:1 links (UNIQUE) with no enforced DB FK**.
- `market_products.market_id` / `market_products.product_id`,
  `orders.scheduled_order_id`, `assistant_conversations.user_id`, and
  `assistant_conversations.market_id` are logical references in the application model only;
  the current migrations do not create DB-level FK constraints for them.
- `price_snapshots` is currently a normal table. Monthly range partitioning is a target
  design described later, not an implemented migration.
- Catalog was normalized: `categories` → **`product_categories`**, and a new
  **`units_of_measurement`** table replaces free-text `products.unit_of_measure`.
  `products` keeps legacy `category`/`unit` text columns during transition and adds
  `category_id` / `unit_id` FKs.
- The Payment & Billing context (`invoices`/`payments`/`refunds`) is superseded by a
  **B2B credit / công nợ** model: **`restaurant_credit`** + **`credit_transactions`**.
- `restaurants` is **1:1 with a user** (no `restaurant_members`); it carries
  `contact_person` + `pickup_start`/`pickup_end` + `status` and has **no** `latitude`,
  `longitude`, `phone`, `is_approved`, or `deleted_at`.
- New tables: `delivery_addresses`, `units_of_measurement`, `product_categories`,
  `restaurant_credit`, `credit_transactions`, `order_issues`, `assistant_conversations`.

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

> _This table reflects the full design. See **Sync Status** above for which entities are
> implemented vs `[PLANNED]`. Newly added implemented entities are listed at the end._

| Entity | Aggregate Root | Owned By Module | Relationship Summary |
|--------|---------------|-----------------|----------------------|
| `roles` | No | Auth | Lookup table for global user roles. Seeded values: `admin`, `operations_manager`, `market_agent`, `hub_staff`, `driver`, `restaurant`. |
| `users` | Yes | Auth | Root of all user accounts. Each user has exactly one global role through `users.role_id`. Optional `phone` can be used as a second login identifier. Logical one-to-one with `restaurants` and `driver_profiles` through unique `user_id` columns, but no DB FK for those profile links. One-to-many with `refresh_tokens` and `notifications`/`notification_devices` (Notifications module; no DB FK — app-level link per DEC-NOT-12). |
| `refresh_tokens` | No | Auth | Many-to-one with `users`. Append-only; each row is an issued token. |
| `user_market_assignments` | No | Auth | Many-to-many join between `users` (market_agent) and `markets`. Enforces market-level access control for Market Agents. |
| `markets` | Yes | Catalog | Root of market registry data. Referenced by `market_products` and `user_market_assignments`; `market_products.market_id` is currently a logical ID, not an enforced DB FK. |
| `products` | Yes | Catalog | System-wide product catalog. Referenced by `market_products`; `market_products.product_id` is currently a logical ID, not an enforced DB FK. |
| `market_products` | No | Pricing | Carries current market/product price and quantity state. DB-enforced one-to-many with `price_snapshots` and `order_items`; logical link to `markets` and `products`. |
| `price_snapshots` | No | Pricing | Append-only audit log. Many-to-one with `market_products`. Current DB table is not partitioned. |
| `system_config` | No | Pricing | `[PLANNED]` Key-value store for Admin-configurable parameters (e.g., cutoff time, price band tolerance). |
| `restaurants` | Yes | Auth | Restaurant profile 1:1 with a user through unique `user_id` (logical link, no DB FK). One-to-many with `orders`, `delivery_addresses`, and credit records. |
| `orders` | Yes | Orders | Aggregate root of the order lifecycle. One-to-many with `order_items`. DB-enforced many-to-one with `restaurants`; `order_group_id` is reserved for planned `order_groups`. |
| `order_items` | No | Orders | Line items. Many-to-one with `orders` and `market_products`. |
| `order_groups` | Yes | Orders | `[PLANNED]` Groups multiple confirmed orders for batch logistics dispatch. |
| `scheduled_orders` | Yes | Orders | Recurring order schedule. DB-enforced many-to-one with `restaurants`; generated `orders.scheduled_order_id` is currently an indexed nullable correlation field, not a DB FK. |
| `hubs` | Yes | Hub | `[PLANNED]` Distribution hub registry. |
| `hub_inventory` | No | Hub | `[PLANNED]` Running stock balance per (hub, market_product). |
| `hub_inbound_events` | No | Hub | `[PLANNED]` Append-heavy inbound tracking records. |
| `hub_outbound_events` | No | Hub | `[PLANNED]` Append-heavy outbound tracking records. |
| `cross_dock_transfers` | No | Hub | `[PLANNED]` Records a direct transfer from inbound delivery to outbound route at a hub. |
| `vehicles` | Yes | Logistics | `[PLANNED]` Vehicle fleet registry. |
| `delivery_routes` | Yes | Logistics | `[PLANNED]` Calculated route with ordered stop list in JSONB. |
| `deliveries` | No | Logistics | `[PLANNED]` One delivery stop per order within a route. |
| `payments` | No | Payment | `[PLANNED/SUPERSEDED]` Original payment model, superseded by B2B credit in current implementation. |
| `refunds` | No | Payment | `[PLANNED/SUPERSEDED]` Original refund model, superseded by B2B credit in current implementation. |
| `driver_profiles` | No | Auth | Extended profile for `driver` role users. Logical one-to-one with `users` through unique `user_id`; no DB FK. |
| `notifications` | No | Notifications | Notification log: content immutable, read + send metadata mutable. `user_id` app-level link (no DB FK, DEC-NOT-12). Consumed integration events → persisted rows (order_status, credit_alert). |
| `notification_devices` | Yes | Notifications | Push token registry (FCM/APNs/web-push). Soft-unregister via `revoked_at`; one active token per (user_id, token). |
| `analytics_aggregations` | No | Analytics | `[PLANNED]` Pre-computed analytics rows. Read by Analytics module only. |
| `export_jobs` | No | Analytics | `[PLANNED]` Async CSV export job tracking. |
| `password_reset_tokens` | No | Auth | _(implemented)_ Append-only password-reset tokens. Many-to-one with `users`. |
| `verification_codes` | No | Auth | _(implemented)_ Email/phone verification codes. Many-to-one with `users`. |
| `delivery_addresses` | No | Auth | _(implemented, NEW)_ Per-restaurant delivery addresses. Many-to-one with `restaurants`. |
| `product_categories` | No | Catalog | _(implemented, NEW)_ Product category lookup (renamed from `categories`). |
| `units_of_measurement` | No | Catalog | _(implemented, NEW)_ Unit lookup; referenced by `products.unit_id`. |
| `order_issues` | No | Orders | _(implemented, NEW)_ Receipt/discrepancy reports. Many-to-one with `orders`/`order_items`. |
| `restaurant_credit` | No | Orders | _(implemented, NEW)_ 1:1 B2B credit account per `restaurant`. |
| `credit_transactions` | No | Orders | _(implemented, NEW)_ Append-only credit ledger. Many-to-one with `restaurants`/`orders`. |
| `assistant_conversations` | No | Assistant | _(implemented, NEW)_ DB-backed AI assistant session store (TTL via `expires_at`). |

### 1.2 Key Relationships

```
roles ──< users
users ──< refresh_tokens
users ──< user_market_assignments >── markets
users ──1 restaurants ──< delivery_addresses
users ──1 restaurants ──< orders ──< order_items >── market_products
restaurants ──< scheduled_orders
restaurants ──1 restaurant_credit
restaurants ──< credit_transactions >── orders
orders ──< order_issues >── order_items
orders ──< order_issues >── users (reported_by)
market_products ──< price_snapshots
```

**Legend:** `──<` = one-to-many, `──1` = one-to-one, `>──` = many-to-one.
This block focuses on implemented tables. Logical-only IDs such as
`market_products.market_id/product_id` and `orders.scheduled_order_id` are described in the
table notes and DBML, not drawn here as enforced FK relationships.

### 1.3 DDD Aggregate Roots

Aggregate roots are entities through which all external access to their cluster must pass. Their IDs are safe to expose in URLs and API responses.

- **Auth:** `users` (and by extension its `refresh_tokens`)
- **Catalog:** `markets`, `products`
- **Pricing:** `market_products` (current price board) and `price_snapshots` as audit history
- **Orders:** `orders` (with `order_items` as internal), `scheduled_orders`, `restaurant_credit`
- **Planned Logistics/Hub:** `delivery_routes`, `vehicles`, `hubs` and related tables are roadmap-only
- **Analytics:** No aggregate roots — read-only cross-module queries

---

## 2. Table Definitions (Implementation-Aligned Logical DDL)

This section documents table intent in SQL-like form for architecture review. It is not a
literal migration script. The exact physical schema is the EF migrations/model snapshot.

Implementation caveats:
- Most UUID primary keys and timestamps are set by the application/domain model, not by
  PostgreSQL `gen_random_uuid()` / `NOW()` defaults.
- Logical examples use `snake_case`; the current EF schema is mixed PascalCase/snake_case.
- Inline `CHECK` constraints document domain invariants; current migrations do not emit them.
- Planned tables remain below for roadmap context and are marked `[PLANNED]`.

```sql
-- ============================================================
-- ENUM TYPE DEFINITIONS
-- ============================================================

-- Current implementation: no PostgreSQL enum types are created for implemented tables.
-- Status columns are VARCHAR via EF HasConversion<string>().
-- Planned-only tables may still show intended enum-like values in comments below.

-- ============================================================
-- AUTH MODULE TABLES
-- ============================================================

-- roles
-- Lookup table for global roles. Role names are stable lowercase strings exposed by the API.
CREATE TABLE roles (
    id              UUID            PRIMARY KEY,
    name            VARCHAR(50)     NOT NULL,
    description     VARCHAR(255)    NOT NULL,
    created_at      TIMESTAMPTZ     NOT NULL,
    CONSTRAINT roles_name_unique UNIQUE (name)
);

-- users
-- Aggregate root for all user accounts.
-- Soft delete: deleted_at IS NOT NULL means the account is deactivated.
-- is_active: used for fast active-status checks without reading deleted_at.
-- role_id: one global role per user through the roles lookup table.
-- phone: optional second login identifier. Phone OTP/SMS verification is deferred in v1.
CREATE TABLE users (
    id                   UUID            PRIMARY KEY,
    email                VARCHAR(255)    NOT NULL,
    phone                VARCHAR(20),
    password_hash        TEXT            NOT NULL,
    full_name            VARCHAR(255),
    avatar_url           VARCHAR(512),
    role_id              UUID            NOT NULL,
    is_active            BOOLEAN         NOT NULL DEFAULT true,
    failed_login_count   INT             NOT NULL DEFAULT 0,   -- FR-AUTH-009: lockout counter
    locked_until         TIMESTAMPTZ,                          -- FR-AUTH-009: NULL = not locked
    email_verified_at    TIMESTAMPTZ,
    created_at           TIMESTAMPTZ     NOT NULL,
    updated_at           TIMESTAMPTZ     NOT NULL,
    deleted_at           TIMESTAMPTZ,
    -- email is unique among non-deleted rows (partial index ON DeletedAt IS NULL);
    -- phone is unique among non-null values (partial index).
    CONSTRAINT fk_users_role
        FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE RESTRICT
);
-- NOTE: no `status` enum (modeled by is_active + deleted_at), no phone_verified_at,
--       no last_login_at in the implemented model.

-- refresh_tokens
-- Append-only — no updated_at or deleted_at.
-- token_hash: stores bcrypt hash of the raw token, never the raw token itself.
-- family_id: groups tokens issued from the same login session for family-wide
--   invalidation on reuse detection (FR-AUTH-007).
-- replaced_by_token_id: links the chain of rotated tokens for audit trail.
-- revoked_at: NULL = still valid. Set to NOW() on use (rotation) or logout.
CREATE TABLE refresh_tokens (
    id                      UUID        PRIMARY KEY,
    user_id                 UUID        NOT NULL,
    token_hash              TEXT        NOT NULL,
    family_id               UUID        NOT NULL,
    expires_at              TIMESTAMPTZ NOT NULL,
    revoked_at              TIMESTAMPTZ,
    replaced_by_token_id    UUID,
    created_at              TIMESTAMPTZ NOT NULL,
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
-- assigned_by is a logical admin/operator user ID; current migrations do not enforce a DB FK.
CREATE TABLE user_market_assignments (
    id          UUID        PRIMARY KEY,
    user_id     UUID        NOT NULL,
    market_id   UUID        NOT NULL,
    assigned_by UUID,
    created_at  TIMESTAMPTZ NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL,
    CONSTRAINT user_market_assignments_unique UNIQUE (user_id, market_id),
    CONSTRAINT fk_uma_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT fk_uma_market
        FOREIGN KEY (market_id) REFERENCES markets (id) ON DELETE CASCADE
);

-- password_reset_tokens
-- Append-only. token_hash stores a hash of the raw reset token.
CREATE TABLE password_reset_tokens (
    id          UUID         PRIMARY KEY,
    user_id     UUID         NOT NULL,
    token_hash  VARCHAR(128) NOT NULL,
    expires_at  TIMESTAMPTZ  NOT NULL,
    used_at     TIMESTAMPTZ,
    created_at  TIMESTAMPTZ  NOT NULL,
    CONSTRAINT password_reset_tokens_token_hash_unique UNIQUE (token_hash),
    CONSTRAINT fk_password_reset_tokens_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);
-- NOTE: no channel / target / requested_ip_hash columns in the implemented model.

-- verification_codes
-- Email/phone verification codes. code_hash stores a hash of the raw code.
CREATE TABLE verification_codes (
    id          UUID         PRIMARY KEY,
    user_id     UUID         NOT NULL,
    channel     VARCHAR(10)  NOT NULL,  -- email | phone
    code_hash   VARCHAR(128) NOT NULL,
    expires_at  TIMESTAMPTZ  NOT NULL,
    used_at     TIMESTAMPTZ,
    created_at  TIMESTAMPTZ  NOT NULL,
    CONSTRAINT fk_verification_codes_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);
-- NOTE: no `target` column in the implemented model.

-- ============================================================
-- CATALOG + PRICING TABLES
-- ============================================================

-- markets
-- Wholesale market definitions. No market seed data is implemented in the current codebase.
CREATE TABLE markets (
    id          UUID            PRIMARY KEY,
    name        TEXT            NOT NULL,
    location    TEXT,
    address     TEXT,
    latitude    NUMERIC(9, 6),
    longitude   NUMERIC(9, 6),
    is_active   BOOLEAN         NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ     NOT NULL,
    updated_at  TIMESTAMPTZ     NOT NULL,
    deleted_at  TIMESTAMPTZ
);

-- product_categories  (NEW — Catalog module; renamed from the planned `categories`)
-- name is unique among non-deleted rows (partial index). No display_order column.
CREATE TABLE product_categories (
    id          UUID         PRIMARY KEY,
    name        VARCHAR(200) NOT NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ  NOT NULL,
    updated_at  TIMESTAMPTZ  NOT NULL,
    deleted_at  TIMESTAMPTZ
);

-- units_of_measurement  (NEW — Catalog module)
-- Replaces the free-text products.unit_of_measure. name unique among non-deleted rows.
CREATE TABLE units_of_measurement (
    id           UUID         PRIMARY KEY,
    name         VARCHAR(100) NOT NULL,
    abbreviation VARCHAR(20),
    is_active    BOOLEAN      NOT NULL DEFAULT true,
    created_at   TIMESTAMPTZ  NOT NULL,
    updated_at   TIMESTAMPTZ  NOT NULL,
    deleted_at   TIMESTAMPTZ
);

-- products
-- System-wide product catalog. Only Admin can create/deactivate products (FR-PRI-006, GA-007).
-- Soft delete: deleted_at IS NOT NULL means the product is inactive (removed from active listings).
-- NOTE: normalized to category_id (-> product_categories, SET NULL) and unit_id
--       (-> units_of_measurement, RESTRICT). Legacy `category`/`unit` text columns are kept
--       during the transition. `created_by` has no DB-level FK. No image_url / is_active columns.
CREATE TABLE products (
    id          UUID          PRIMARY KEY,
    category_id UUID,                                  -- nullable
    unit_id     UUID          NOT NULL,
    name        VARCHAR(200)  NOT NULL,
    description VARCHAR(1000),
    category    TEXT,                                  -- LEGACY (transition)
    unit        TEXT,                                  -- LEGACY (transition)
    created_by  UUID,
    created_at  TIMESTAMPTZ   NOT NULL,
    updated_at  TIMESTAMPTZ   NOT NULL,
    deleted_at  TIMESTAMPTZ,
    CONSTRAINT fk_products_category
        FOREIGN KEY (category_id) REFERENCES product_categories (id) ON DELETE SET NULL,
    CONSTRAINT fk_products_unit
        FOREIGN KEY (unit_id) REFERENCES units_of_measurement (id) ON DELETE RESTRICT
);

-- market_products
-- Join entity: tracks which products are available at which market, with current price and quantity.
-- current_price / current_quantity: live state managed by Market Agent.
-- reserved_quantity: tracks soft-reserved quantity (decremented from available for pending orders).
--   The application layer computes available = current_quantity - reserved_quantity.
-- updated_by: last Market Agent user who touched this row.
-- Implementation note: current EF maps deleted_at and uses it in the partial unique index.
-- market_id/product_id are logical cross-module IDs; current migrations do not enforce DB FKs
-- to markets/products.
CREATE TABLE market_products (
    id                  UUID            PRIMARY KEY,
    market_id           UUID            NOT NULL,
    product_id          UUID            NOT NULL,
    current_price       NUMERIC(12, 2)  NOT NULL CHECK (current_price >= 0),
    current_quantity    INTEGER         NOT NULL CHECK (current_quantity >= 0),
    reserved_quantity   INTEGER         NOT NULL DEFAULT 0 CHECK (reserved_quantity >= 0),
    updated_by          UUID,
    created_at          TIMESTAMPTZ     NOT NULL,
    updated_at          TIMESTAMPTZ     NOT NULL,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT market_products_unique UNIQUE (market_id, product_id)
);

-- price_snapshots
-- Append-only, high-frequency write table. No updated_at or deleted_at.
-- Current implementation creates a normal table, not a partitioned table.
-- Design decision: no soft delete — price history is immutable by design (FR-PRI-004).
-- recorded_by is a logical Market Agent user ID; current migrations do not enforce a DB FK to users.
CREATE TABLE price_snapshots (
    id                  UUID            NOT NULL,
    market_product_id   UUID            NOT NULL,
    price               NUMERIC(12, 2)  NOT NULL CHECK (price >= 0),
    quantity            INTEGER         NOT NULL CHECK (quantity >= 0),
    recorded_by         UUID,
    recorded_at         TIMESTAMPTZ     NOT NULL,
    CONSTRAINT fk_price_snapshots_market_product
        FOREIGN KEY (market_product_id) REFERENCES market_products (id) ON DELETE CASCADE
);

-- system_config  [PLANNED — not yet implemented]
-- Key-value store for Admin-configurable runtime parameters.
-- Example keys: 'daily_order_cutoff_time' = '22:00', 'price_band_tolerance_percent' = '10.00'
-- No soft delete — config rows are overwritten in place; audit is covered by updated_at + updated_by.
CREATE TABLE system_config (
    id          UUID        PRIMARY KEY,
    key         TEXT        NOT NULL,
    value       TEXT        NOT NULL,
    description TEXT,
    updated_by  UUID,
    created_at  TIMESTAMPTZ NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL,
    CONSTRAINT system_config_key_unique UNIQUE (key),
    CONSTRAINT fk_system_config_updated_by
        FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE SET NULL
);

-- ============================================================
-- ORDERS MODULE TABLES
-- ============================================================

-- restaurants  (owned by Auth module; read by Orders)
-- One restaurant account per user (UNIQUE on user_id).
-- status: 'pending' = cannot place orders until approved (GA-009). Stored lowercase via EF.
-- NOTE: the implemented table replaced is_approved with `status`, dropped latitude/longitude/
--       phone/deleted_at, and added contact_person + pickup_start/pickup_end.
--       Per-restaurant delivery locations now live in `delivery_addresses`.
CREATE TABLE restaurants (
    id             UUID         PRIMARY KEY,
    user_id        UUID         NOT NULL,
    name           VARCHAR(200) NOT NULL,
    address        VARCHAR(500),
    contact_person VARCHAR(200),
    pickup_start   TIME,
    pickup_end     TIME,
    status         VARCHAR(20)  NOT NULL DEFAULT 'pending',  -- pending | active | suspended
    created_at     TIMESTAMPTZ  NOT NULL,
    updated_at     TIMESTAMPTZ  NOT NULL,
    CONSTRAINT restaurants_user_id_unique UNIQUE (user_id)
    -- NOTE: user_id is a logical 1:1 link; no DB-level FK to users is created by migrations.
);

-- delivery_addresses  (NEW — Auth module)
-- Per-restaurant delivery destinations. is_default marks the primary address.
CREATE TABLE delivery_addresses (
    id             UUID         PRIMARY KEY,
    restaurant_id  UUID         NOT NULL,
    recipient_name TEXT,
    phone          VARCHAR(20),
    address_line   TEXT         NOT NULL,
    latitude       NUMERIC(9, 6),
    longitude      NUMERIC(9, 6),
    is_default     BOOLEAN      NOT NULL DEFAULT false,
    created_at     TIMESTAMPTZ  NOT NULL,
    updated_at     TIMESTAMPTZ  NOT NULL,
    deleted_at     TIMESTAMPTZ,
    CONSTRAINT fk_delivery_addresses_restaurant
        FOREIGN KEY (restaurant_id) REFERENCES restaurants (id) ON DELETE CASCADE
);

-- order_groups  [PLANNED — not yet implemented]
-- Administrative grouping of confirmed orders for batch logistics dispatch.
-- orders.order_group_id already exists as a nullable column reserved for this (no FK yet).
-- total_orders: denormalized count, updated by the application when orders are added/removed.
-- Design decision: grouped_by references the admin user who created the group.
CREATE TABLE order_groups (
    id              UUID                PRIMARY KEY,
    name            TEXT,
    status          order_group_status  NOT NULL DEFAULT 'open',
    grouped_by      UUID,
    total_orders    INTEGER             NOT NULL DEFAULT 0 CHECK (total_orders >= 0),
    created_at      TIMESTAMPTZ         NOT NULL,
    updated_at      TIMESTAMPTZ         NOT NULL,
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT fk_order_groups_grouped_by
        FOREIGN KEY (grouped_by) REFERENCES users (id) ON DELETE SET NULL
);

-- orders
-- Aggregate root of the order lifecycle.
-- order_group_id: nullable — NULL means not yet batched.
-- scheduled_order_id: nullable — NULL means one-off order; set if generated from a scheduled_orders record.
--   Current migrations create an index but no DB-level FK to scheduled_orders.
-- payment_status: tracks payment lifecycle separately from order status.
-- cancelled_at: set when status transitions to 'cancelled'.
-- total_amount: maintained by application on order_item creation/update. Denormalized for fast reads.
-- Design decision: ON DELETE RESTRICT on restaurant_id to prevent accidental deletion of a restaurant
--   with active orders. Use soft delete on restaurant instead.
-- Design decision: order_group_id uses ON DELETE SET NULL — an order is ungrouped if its group is deleted.
-- NOTE: status/payment_status are stored as VARCHAR via EF (PascalCase), not PG enums.
--   status: Draft | Confirmed | Batched | PickedUp | AtHub | Delivering | Delivered | Cancelled
--   payment_status: NotApplicable | Outstanding | Settled | Waived
--   order_group_id is a nullable column reserved for the PLANNED order_groups table (no FK yet).
--   `updated_at` is a concurrency token. confirmed_receipt_at: set when the restaurant confirms receipt.
--   Not implemented: order_number, created_by/confirmed_by/cancelled_by, delivery_address_snapshot,
--   order_date, target_delivery_date, subtotal_amount, cutoff_at.
CREATE TABLE orders (
    id                   UUID            PRIMARY KEY,
    restaurant_id        UUID            NOT NULL,
    order_group_id       UUID,
    scheduled_order_id   UUID,
    status               VARCHAR(20)     NOT NULL,
    payment_status       VARCHAR(20)     NOT NULL,
    scheduled_for        TIMESTAMPTZ,
    confirmed_receipt_at TIMESTAMPTZ,
    total_amount         NUMERIC(14, 2)  NOT NULL CHECK (total_amount >= 0),
    notes                TEXT,
    cancelled_at         TIMESTAMPTZ,
    cancellation_reason  TEXT,
    created_at           TIMESTAMPTZ     NOT NULL,
    updated_at           TIMESTAMPTZ     NOT NULL,
    deleted_at           TIMESTAMPTZ,
    CONSTRAINT fk_orders_restaurant
        FOREIGN KEY (restaurant_id) REFERENCES restaurants (id) ON DELETE RESTRICT
);

-- order_items
-- Line items for an order.
-- unit_price: price at order creation (DRAFT) time — may differ from locked_unit_price.
-- product_name_snapshot: product name at order creation time, preserved for history even if product is renamed.
-- locked_unit_price: set when order transitions to CONFIRMED (after payment). Immune to subsequent
--   market price changes. NULL until confirmed. This is what the restaurant actually pays.
-- locked_total: quantity × locked_unit_price. Set at CONFIRMED time.
-- actual_quantity: actual quantity delivered; may be less than quantity if Hub Staff flags shortage.
-- No subtotal generated column in the implemented model; totals are maintained in application code.
-- Design decision: no deleted_at. If an item must be removed, the order itself is cancelled.
--   Individual item cancellation is not a v1 feature.
-- Design decision: ON DELETE RESTRICT on market_product_id to preserve order integrity.
CREATE TABLE order_items (
    id                      UUID            PRIMARY KEY,
    order_id                UUID            NOT NULL,
    market_product_id       UUID            NOT NULL,
    product_name_snapshot   VARCHAR(200)    NOT NULL,
    quantity                INTEGER         NOT NULL CHECK (quantity > 0),
    unit_price              NUMERIC(12, 2)  NOT NULL CHECK (unit_price >= 0),
    locked_unit_price       NUMERIC(12, 2),
    locked_total            NUMERIC(14, 2),
    actual_quantity         NUMERIC(10, 2),
    created_at              TIMESTAMPTZ     NOT NULL,
    updated_at              TIMESTAMPTZ     NOT NULL,
    CONSTRAINT fk_order_items_order
        FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_order_items_market_product
        FOREIGN KEY (market_product_id) REFERENCES market_products (id) ON DELETE RESTRICT
);

-- scheduled_orders
-- Recurring order template (FR-ORD-002). Generates concrete order instances on each tick.
-- recurrence_type: stored as VARCHAR(20) via EF (PascalCase: 'Daily' | 'Weekly'); no DB CHECK.
-- first_run_at: when the first instance should be generated (in Asia/Ho_Chi_Minh tz).
-- last_executed_at: updated by the background job after each successful instance generation.
--   Used to enforce idempotency — the job checks this before generating a new instance.
-- cancelled_at: set when the restaurant or admin cancels the recurring schedule.
CREATE TABLE scheduled_orders (
    id                  UUID        PRIMARY KEY,
    restaurant_id       UUID        NOT NULL,
    recurrence_type     VARCHAR(20) NOT NULL,   -- Daily | Weekly (PascalCase via EF, no DB CHECK)
    first_run_at        TIMESTAMPTZ NOT NULL,
    last_executed_at    TIMESTAMPTZ,
    cancelled_at        TIMESTAMPTZ,
    notes               TEXT,
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT fk_scheduled_orders_restaurant
        FOREIGN KEY (restaurant_id) REFERENCES restaurants (id) ON DELETE RESTRICT
);
-- NOTE: not implemented — created_by_user_id, name, weekdays, items_json, next_run_at,
--       timezone, is_active.

-- order_issues  (NEW — Orders module)
-- Receipt/discrepancy reports raised against delivered orders.
CREATE TABLE order_issues (
    id                UUID          PRIMARY KEY,
    order_id          UUID          NOT NULL,
    order_item_id     UUID,                                  -- nullable
    issue_type        VARCHAR(20)   NOT NULL,   -- missing | wrong | damaged (lowercase via EF)
    affected_quantity NUMERIC(10,2) NOT NULL,
    description       VARCHAR(1000) NOT NULL,
    status            VARCHAR(20)   NOT NULL,   -- open | resolved (lowercase via EF)
    reported_by       UUID          NOT NULL,
    resolved_at       TIMESTAMPTZ,
    created_at        TIMESTAMPTZ   NOT NULL,
    updated_at        TIMESTAMPTZ   NOT NULL,
    deleted_at        TIMESTAMPTZ,
    CONSTRAINT fk_order_issues_order
        FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT fk_order_issues_order_item
        FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE SET NULL,
    CONSTRAINT fk_order_issues_reported_by
        FOREIGN KEY (reported_by) REFERENCES users (id) ON DELETE RESTRICT
);

-- ============================================================
-- B2B CREDIT / CÔNG NỢ TABLES  (Orders module)
-- Supersedes the planned invoices/payments/refunds gateway model in v1.
-- ============================================================

-- restaurant_credit
-- 1:1 credit account per restaurant. updated_at is a concurrency token.
CREATE TABLE restaurant_credit (
    restaurant_id       UUID          PRIMARY KEY,
    credit_limit        NUMERIC(14,2) NOT NULL,
    outstanding_balance NUMERIC(14,2) NOT NULL,
    updated_at          TIMESTAMPTZ   NOT NULL,
    CONSTRAINT fk_restaurant_credit_restaurant
        FOREIGN KEY (restaurant_id) REFERENCES restaurants (id) ON DELETE RESTRICT
);

-- credit_transactions
-- Append-only ledger of credit movements.
CREATE TABLE credit_transactions (
    id            UUID          PRIMARY KEY,
    restaurant_id UUID          NOT NULL,
    order_id      UUID,                                   -- nullable
    type          VARCHAR(20)   NOT NULL,  -- charge | settlement | refund | adjustment (lowercase via EF)
    amount        NUMERIC(14,2) NOT NULL,
    balance_after NUMERIC(14,2) NOT NULL,
    note          VARCHAR(500),
    created_at    TIMESTAMPTZ   NOT NULL,
    CONSTRAINT fk_credit_transactions_restaurant
        FOREIGN KEY (restaurant_id) REFERENCES restaurants (id) ON DELETE RESTRICT,
    CONSTRAINT fk_credit_transactions_order
        FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT
);

-- ============================================================
-- AI SHOPPING ASSISTANT  (API host)
-- ============================================================

-- assistant_conversations
-- DB-backed conversation store for the AI assistant. state holds serialized session
-- context; rows expire via expires_at (TTL cleanup).
-- user_id and market_id are logical context IDs only; current migrations do not enforce DB FKs.
CREATE TABLE assistant_conversations (
    id          UUID         PRIMARY KEY,
    user_id     UUID         NOT NULL,
    market_id   UUID,                                    -- nullable
    session_id  VARCHAR(128) NOT NULL,
    state       JSONB        NOT NULL,
    expires_at  TIMESTAMPTZ  NOT NULL,
    created_at  TIMESTAMPTZ  NOT NULL,
    updated_at  TIMESTAMPTZ  NOT NULL,
    CONSTRAINT assistant_conversations_session_id_unique UNIQUE (session_id)
);

-- ============================================================
-- HUB MODULE TABLES   [PLANNED — none of the tables below are implemented yet]
-- ============================================================

-- hubs
-- Distribution hub registry. Managed by Admin.
-- capacity_kg: maximum storage capacity.
-- managed_by: admin user responsible for this hub.
CREATE TABLE hubs (
    id          UUID            PRIMARY KEY,
    name        TEXT            NOT NULL,
    address     TEXT,
    latitude    NUMERIC(9, 6),
    longitude   NUMERIC(9, 6),
    capacity_kg NUMERIC(10, 2)  CHECK (capacity_kg > 0),
    is_active   BOOLEAN         NOT NULL DEFAULT true,
    managed_by  UUID,
    created_at  TIMESTAMPTZ     NOT NULL,
    updated_at  TIMESTAMPTZ     NOT NULL,
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
    id                  UUID        PRIMARY KEY,
    hub_id              UUID        NOT NULL,
    market_product_id   UUID        NOT NULL,
    quantity_in         INTEGER     NOT NULL DEFAULT 0 CHECK (quantity_in >= 0),
    quantity_out        INTEGER     NOT NULL DEFAULT 0 CHECK (quantity_out >= 0),
    quantity_available  INTEGER     GENERATED ALWAYS AS (quantity_in - quantity_out) STORED,
    recorded_at         TIMESTAMPTZ NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
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
    id                  UUID        PRIMARY KEY,
    hub_id              UUID        NOT NULL,
    source_market_id    UUID,
    delivery_route_id   UUID,
    items               JSONB       NOT NULL DEFAULT '[]',
    total_quantity_kg   NUMERIC(10, 2),
    arrived_at          TIMESTAMPTZ NOT NULL,
    recorded_by         UUID,
    hub_staff_user_id   UUID,
    condition_status    VARCHAR(20) NOT NULL DEFAULT 'OK'
                            CHECK (condition_status IN ('OK', 'DAMAGED', 'MISSING', 'PARTIAL')),
    discrepancy_notes   TEXT,
    created_at          TIMESTAMPTZ NOT NULL,
    updated_at          TIMESTAMPTZ NOT NULL,
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
    id                      UUID        PRIMARY KEY,
    hub_id                  UUID        NOT NULL,
    destination_route_id    UUID,
    items                   JSONB       NOT NULL DEFAULT '[]',
    total_quantity_kg       NUMERIC(10, 2),
    dispatched_at           TIMESTAMPTZ NOT NULL,
    recorded_by             UUID,
    created_at              TIMESTAMPTZ NOT NULL,
    updated_at              TIMESTAMPTZ NOT NULL,
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
    id                      UUID        PRIMARY KEY,
    hub_id                  UUID        NOT NULL,
    inbound_event_id        UUID        NOT NULL,
    outbound_route_id       UUID        NOT NULL,
    status                  TEXT        NOT NULL DEFAULT 'pending'
                                CHECK (status IN ('pending', 'in_progress', 'completed')),
    notes                   TEXT,
    created_at              TIMESTAMPTZ NOT NULL,
    updated_at              TIMESTAMPTZ NOT NULL,
    deleted_at              TIMESTAMPTZ,
    CONSTRAINT fk_cross_dock_hub
        FOREIGN KEY (hub_id) REFERENCES hubs (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cross_dock_inbound
        FOREIGN KEY (inbound_event_id) REFERENCES hub_inbound_events (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cross_dock_route
        FOREIGN KEY (outbound_route_id) REFERENCES delivery_routes (id) ON DELETE RESTRICT
);

-- ============================================================
-- PAYMENT MODULE TABLES   [PLANNED — payments & refunds not implemented;
--   the v1 billing model is restaurant_credit + credit_transactions above.
--   (driver_profiles below IS implemented.)]
-- ============================================================

-- payments  [PLANNED]
-- Tracks per-order payment transactions via external payment gateway.
-- order_id: no FK constraint — cross-context reference by ID only (DDD rule).
-- gateway_transaction_id: the gateway's reference ID for reconciliation.
-- gateway_response: full gateway callback payload stored for audit.
CREATE TABLE payments (
    id                      UUID            PRIMARY KEY,
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
    created_at              TIMESTAMPTZ     NOT NULL,
    updated_at              TIMESTAMPTZ     NOT NULL
);

CREATE INDEX idx_payments_order_id ON payments (order_id);
CREATE INDEX idx_payments_restaurant_id ON payments (restaurant_id);
CREATE INDEX idx_payments_status ON payments (status) WHERE status = 'PENDING';

-- refunds  [PLANNED]
-- Tracks partial or full refunds issued against a payment.
-- Triggered by: hub discrepancy flags, order cancellation after payment.
-- reason: 'hub_shortage', 'hub_damage', 'customer_cancel', 'system'
CREATE TABLE refunds (
    id              UUID            PRIMARY KEY,
    payment_id      UUID            NOT NULL,
    order_id        UUID            NOT NULL,
    reason          VARCHAR(255)    NOT NULL
                        CHECK (reason IN ('hub_shortage', 'hub_damage', 'customer_cancel', 'system')),
    amount          NUMERIC(14, 2)  NOT NULL CHECK (amount > 0),
    status          VARCHAR(20)     NOT NULL DEFAULT 'PENDING'
                        CHECK (status IN ('PENDING', 'COMPLETED', 'FAILED')),
    refunded_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ     NOT NULL,
    updated_at      TIMESTAMPTZ     NOT NULL,
    CONSTRAINT fk_refunds_payment
        FOREIGN KEY (payment_id) REFERENCES payments (id) ON DELETE RESTRICT
);

CREATE INDEX idx_refunds_payment_id ON refunds (payment_id);
CREATE INDEX idx_refunds_order_id ON refunds (order_id);

-- driver_profiles  (implemented — Auth module)
-- Extended profile for users with role 'driver'. Logical 1:1 with users via unique user_id.
-- NOTE: the implemented table carries license_plate + phone_number only; it has no
--       current_vehicle_id or status column (those belonged to the planned Logistics design).
CREATE TABLE driver_profiles (
    id            UUID        PRIMARY KEY,
    user_id       UUID        UNIQUE NOT NULL,
    license_plate VARCHAR(20),
    phone_number  VARCHAR(20),
    created_at    TIMESTAMPTZ NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL
);

-- ============================================================
-- LOGISTICS MODULE TABLES   [PLANNED — vehicles, delivery_routes, deliveries not implemented]
-- ============================================================

-- vehicles
-- Vehicle fleet registry. Managed by Admin.
-- plate_number: unique physical identifier for the vehicle.
-- is_available: false when assigned to an active route; enforced by application.
CREATE TABLE vehicles (
    id              UUID            PRIMARY KEY,
    plate_number    TEXT            NOT NULL,
    capacity_kg     NUMERIC(10, 2)  NOT NULL CHECK (capacity_kg > 0),
    vehicle_type    TEXT            NOT NULL,
    is_available    BOOLEAN         NOT NULL DEFAULT true,
    registered_by   UUID,
    created_at      TIMESTAMPTZ     NOT NULL,
    updated_at      TIMESTAMPTZ     NOT NULL,
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
    id                          UUID            PRIMARY KEY,
    vehicle_id                  UUID,
    order_group_id              UUID,
    status                      route_status    NOT NULL DEFAULT 'planned',
    route_metadata              JSONB,
    total_distance_km           NUMERIC(8, 2)   CHECK (total_distance_km >= 0),
    estimated_duration_minutes  INTEGER         CHECK (estimated_duration_minutes >= 0),
    actual_start_at             TIMESTAMPTZ,
    actual_end_at               TIMESTAMPTZ,
    created_by                  UUID,
    created_at                  TIMESTAMPTZ     NOT NULL,
    updated_at                  TIMESTAMPTZ     NOT NULL,
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
    id                  UUID            PRIMARY KEY,
    delivery_route_id   UUID            NOT NULL,
    order_id            UUID            NOT NULL,
    sequence_number     INTEGER         NOT NULL CHECK (sequence_number > 0),
    status              delivery_status NOT NULL DEFAULT 'pending',
    estimated_arrival   TIMESTAMPTZ,
    actual_arrival      TIMESTAMPTZ,
    failure_reason      TEXT,
    created_at          TIMESTAMPTZ     NOT NULL,
    updated_at          TIMESTAMPTZ     NOT NULL,
    CONSTRAINT deliveries_order_id_unique UNIQUE (order_id),
    CONSTRAINT fk_deliveries_route
        FOREIGN KEY (delivery_route_id) REFERENCES delivery_routes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_deliveries_order
        FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT
);

-- ============================================================
-- NOTIFICATIONS MODULE TABLES   [IMPLEMENTED 2026-07-08/09 — epic NOT SCRUM-300]
-- Migrations: 20260708143833_AddNotificationDevices, 20260708151450_AddNotifications,
--             AddNotificationSendStatus. See docs/features/notifications/AUDIT-2026-07-08-not-backend-plan.md.
-- ============================================================

-- notifications
-- Content is append-only/immutable (title/body/payload/type/user_id/created_at);
-- read metadata (is_read/read_at) and send metadata (send_status/attempt_count/
-- last_attempt_at/failed_reason) are the only mutable columns (DEC-NOT-11).
-- No updated_at or deleted_at.
-- payload: event-specific JSON data. Schema varies by type — see Section 4.2.
-- NOTE: `type` is stored as VARCHAR(50) via EF HasConversion<string>() (snake_case),
--   NOT a PG enum type — repo-wide convention (no PG enum types).
-- NOTE: `user_id` is a plain indexed UUID with NO DB-level FK to users(id).
--   Per DEC-NOT-12 the modular-monolith boundary forbids a cross-module FK;
--   referential integrity is enforced at the application layer (recipient resolver +
--   validation). The earlier fk_notifications_user constraint was aspirational only.
CREATE TABLE notifications (
    id               UUID           PRIMARY KEY,
    user_id          UUID           NOT NULL,
    type             VARCHAR(50)    NOT NULL,   -- order_status | credit_alert | system
    title            TEXT           NOT NULL,
    body             TEXT           NOT NULL,
    payload          JSONB,
    is_read          BOOLEAN        NOT NULL DEFAULT false,
    read_at          TIMESTAMPTZ,
    send_status      VARCHAR(20)    NOT NULL DEFAULT 'pending',  -- pending | sent | failed
    attempt_count    INTEGER        NOT NULL DEFAULT 0,
    last_attempt_at  TIMESTAMPTZ,
    failed_reason    TEXT,
    created_at       TIMESTAMPTZ    NOT NULL
);
CREATE INDEX idx_notifications_user_id ON notifications (user_id);
CREATE INDEX idx_notifications_user_read_created
    ON notifications (user_id, is_read, created_at, id);   -- list (cursor) + unread filter
CREATE INDEX idx_notifications_retry_scan
    ON notifications (attempt_count, last_attempt_at)
    WHERE send_status = 'failed';                            -- partial index for retry job scan

-- notification_devices
-- Push token registry (FCM/APNs/web-push). Added by SCRUM-327 (not in original docs/03).
-- Soft-unregister via revoked_at (audit + prevents token reuse). user_id = plain indexed
-- UUID, no cross-module FK (DEC-NOT-12). Dedup: one active token per (user_id, token).
CREATE TABLE notification_devices (
    id          UUID           PRIMARY KEY,
    user_id     UUID           NOT NULL,
    token       TEXT           NOT NULL,       -- FCM/APNs/web-push token
    platform    VARCHAR(20)    NOT NULL,       -- ios | android | web
    device_id   TEXT,                          -- client-supplied, optional
    created_at  TIMESTAMPTZ    NOT NULL,
    updated_at  TIMESTAMPTZ    NOT NULL,
    revoked_at  TIMESTAMPTZ                     -- soft-unregister
);
CREATE INDEX idx_notification_devices_user_id ON notification_devices (user_id);
CREATE UNIQUE INDEX ux_notification_devices_user_token_active
    ON notification_devices (user_id, token)
    WHERE revoked_at IS NULL;                    -- idempotent upsert on re-register

-- ============================================================
-- ANALYTICS MODULE TABLES   [PLANNED — not implemented]
-- ============================================================

-- analytics_aggregations
-- Pre-computed aggregation rows written by the AnalyticsAggregationJob background job.
-- type: describes what kind of aggregation (e.g., 'price_trend_daily', 'demand_heatmap_hourly').
-- period_start / period_end: the time window covered by the aggregation.
-- data_json: serialized aggregation result (structure depends on type).
CREATE TABLE analytics_aggregations (
    id              UUID        PRIMARY KEY,
    type            TEXT        NOT NULL,
    period_start    TIMESTAMPTZ NOT NULL,
    period_end      TIMESTAMPTZ NOT NULL,
    data_json       JSONB       NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL,
    CONSTRAINT analytics_aggregations_unique UNIQUE (type, period_start, period_end)
);

-- export_jobs
-- Tracks async CSV export jobs (FR-ANA-004).
-- status: 'pending', 'processing', 'ready', 'failed'.
-- file_path: local temp storage path; application purges after 24 hours.
-- expires_at: when the export file is no longer available for download.
CREATE TABLE export_jobs (
    id              UUID        PRIMARY KEY,
    requested_by    UUID,
    export_type     TEXT        NOT NULL,
    status          TEXT        NOT NULL DEFAULT 'pending'
                        CHECK (status IN ('pending', 'processing', 'ready', 'failed')),
    parameters      JSONB,
    file_path       TEXT,
    error_message   TEXT,
    ready_at        TIMESTAMPTZ,
    expires_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL,
    CONSTRAINT fk_export_jobs_requested_by
        FOREIGN KEY (requested_by) REFERENCES users (id) ON DELETE SET NULL
);
```

> **Note on DDL ordering:** The SQL block above is architecture documentation, not the
> executable migration source. EF migrations define the actual table creation order and
> DB constraints. Planned tables are kept in the block only for roadmap context.

> **No triggers policy:** current migrations do not create PostgreSQL triggers. See
> [Section 4.4](#44-no-triggers-policy) for the current implementation note.

---

## 3. Index Strategy

This list is based on the current EF model snapshot. It intentionally excludes planned
Hub/Logistics/Notifications/Analytics indexes until those tables exist.

| Table | Current indexes / unique constraints |
|-------|--------------------------------------|
| `roles` | unique `Name` |
| `users` | unique `Email` with filter `"DeletedAt" IS NULL`; unique `Phone` with filter `"Phone" IS NOT NULL`; index `RoleId` |
| `refresh_tokens` | unique `TokenHash`; indexes `FamilyId`, `ReplacedByTokenId`, `UserId` |
| `password_reset_tokens` | unique `TokenHash`; index `UserId` |
| `verification_codes` | indexes `CodeHash`, `UserId` |
| `user_market_assignments` | unique `(UserId, MarketId)`; index `MarketId` |
| `driver_profiles` | unique `UserId` |
| `restaurants` | unique `UserId` |
| `delivery_addresses` | index `RestaurantId` |
| `product_categories` | index `IsActive`; unique `Name` with filter `"DeletedAt" IS NULL` |
| `units_of_measurement` | index `IsActive`; unique `Name` with filter `"DeletedAt" IS NULL` |
| `products` | indexes `CategoryId`, `UnitId`, `Name`, `DeletedAt` |
| `markets` | index `IsActive` (`idx_markets_is_active`); index `DeletedAt` |
| `market_products` | index `MarketId`; unique `(MarketId, ProductId)` with filter `"deleted_at" IS NULL`; index `deleted_at` |
| `price_snapshots` | indexes `MarketProductId`, `RecordedAt` |
| `orders` | indexes `DeletedAt`, `OrderGroupId`, `ScheduledFor`, `ScheduledOrderId`, `Status`, `(RestaurantId, Status)` |
| `order_items` | indexes `OrderId`, `MarketProductId` |
| `scheduled_orders` | indexes `RestaurantId`, `deleted_at` |
| `order_issues` | indexes `OrderId`, `OrderItemId`, `Status`, `deleted_at` |
| `restaurant_credit` | primary key `RestaurantId`; no extra secondary index |
| `credit_transactions` | indexes `RestaurantId`, `OrderId`, `CreatedAt` |
| `assistant_conversations` | unique `SessionId`; index `ExpiresAt` |

Potential future indexes such as `(MarketProductId, RecordedAt DESC)`,
filtered order indexes, Redis-backed reservation indexes, and analytics aggregation indexes
belong in a performance backlog or planned-schema section, not in the current DB ERD.

---

## 4. PostgreSQL-Specific Features

### 4.1 Table Partitioning — `price_snapshots`

**Current status:** not implemented. EF migrations create `price_snapshots` as a normal
PostgreSQL table with primary key `Id` and indexes on `MarketProductId` and `RecordedAt`.
There is no migration DDL for `PARTITION BY RANGE`, no child partitions, and no implemented
`PartitionMaintenanceJob` / `AnalyticsAggregationJob` that creates monthly partitions.

**Target design:** if write volume makes it necessary, range partitioning by `recorded_at`
can be introduced in a dedicated migration. That migration must handle PostgreSQL's unique
constraint requirement for partitioned tables and update EF/migration documentation at the
same time.

---

### 4.2 JSONB Columns

Current implemented JSONB usage is `assistant_conversations.state`, which stores the
assistant session state for a `session_id`. The examples below are planned Logistics /
Notifications design notes unless explicitly implemented later.

#### `assistant_conversations.state` _(implemented)_

Stores serialized assistant session state. The table is keyed by `session_id` and cleaned
up by `expires_at`; `user_id` and `market_id` are context identifiers, not enforced FKs.

#### `delivery_routes.route_metadata` _(planned)_

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

> **Implemented today (epic NOT SCRUM-300):** `order_status`, `credit_alert`, `system`.
> `price_change` and `delivery_update` are `[PLANNED]` — blocked until the Pricing and
> Logistics epics emit the corresponding integration events into `FreshFlow.Contracts`
> (DEC-NOT-03); no consumer persists them yet.

**Schema by notification_type:**

```json
// type: 'price_change'   [PLANNED — blocked by Pricing epic]
{
  "market_id": "uuid",
  "product_id": "uuid",
  "market_product_id": "uuid",
  "previous_price": "125000.00",
  "new_price": "135000.00",
  "new_quantity": "420",
  "updated_at": "2026-05-10T04:10:00+07:00"
}

// type: 'order_status'  (confirmed — from OrderConfirmedIntegrationEvent)
{
  "order_id": "uuid",
  "new_status": "confirmed",
  "total_amount": 1250000.00,
  "occurred_at": "2026-05-10T04:10:00+07:00"
}
// type: 'order_status'  (cancelled — from OrderCancelledIntegrationEvent)
{
  "order_id": "uuid",
  "new_status": "cancelled",
  "cancellation_reason": "out_of_stock",
  "occurred_at": "2026-05-10T04:10:00+07:00"
}

// type: 'delivery_update'   [PLANNED — blocked by Logistics epic]
{
  "delivery_route_id": "uuid",
  "order_id": "uuid",
  "delivery_status": "delivered",
  "actual_arrival": "2026-05-10T05:47:00+07:00",
  "estimated_arrival": "2026-05-10T06:00:00+07:00"
}

// type: 'credit_alert'
{
  "level": "warning",              // "warning" | "exceeded"
  "utilization": 0.85,
  "outstanding": 8500000.00,
  "limit": 10000000.00
}

// type: 'system'
{
  "event_code": "MAINTENANCE_WINDOW",
  "message_vi": "Hệ thống sẽ bảo trì từ 01:00–03:00 ngày mai.",
  "message_en": "System maintenance scheduled 01:00–03:00 tomorrow."
}
```

#### `hub_inbound_events.items` and `hub_outbound_events.items` _(planned)_

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

Current implementation does not create PostgreSQL enum types for implemented tables.
Auth roles are stored in the `roles` lookup table; other status-like fields are `varchar`
through EF `HasConversion<string>()`.

| Column | Storage | Current values / note |
|--------|---------|-----------------------|
| `orders.status` | `varchar(20)` | `Draft`, `Confirmed`, `Batched`, `PickedUp`, `AtHub`, `Delivering`, `Delivered`, `Cancelled` |
| `orders.payment_status` | `varchar(20)` | `NotApplicable`, `Outstanding`, `Settled`, `Waived` |
| `scheduled_orders.recurrence_type` | `varchar(20)` | `Daily`, `Weekly` |
| `restaurants.status` | `varchar(20)` | lowercase via EF conversion: `pending`, `active`, `suspended` |
| `order_issues.issue_type` / `status` | `varchar(20)` | lowercase via EF conversion |
| `credit_transactions.type` | `varchar(20)` | lowercase via EF conversion |

Planned tables may still use enum-like values in design notes, but they are not part of the
current physical schema.

---

### 4.4 No Triggers Policy

Current migrations do not create PostgreSQL triggers. Business invariants such as order
status transitions, total calculation, receipt issue handling, credit balance updates, and
price board cache writes live in the application layer and EF transactions.

Planned Hub/Logistics computed-state examples should stay in their module design docs until
those tables and services are implemented.

---

## 5. Migration Strategy

### 5.1 Tool and Configuration

- **ORM:** EF Core 10 with Npgsql provider for PostgreSQL
- **Migration runner:** `AdminSeeder` is registered as an `IHostedService` and calls
  `db.Database.MigrateAsync()` before seeding roles/admin. `Program.cs` does not call
  `MigrateAsync()` directly.
- **Migration location:** `src/FreshFlow.Infrastructure.Persistence/Migrations/`
- **EF Core model configuration:** each module defines `IEntityTypeConfiguration<T>` classes
  in its infrastructure project, for example
  `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Configurations/`.

### 5.2 Naming Convention

```
{YYYYMMDD}_{HHMMSS}_{PascalCaseDescription}
```

Examples:
```
20260531173716_InitAuth
20260606152229_AddRolesTable
20260609082728_CatalogModuleInit
20260612121011_PricingModuleInit
20260617150332_OrdersModuleInit
```

### 5.3 Dev Seed Data

The current dedicated idempotent seeder is `AdminSeeder`. It runs migrations, then checks
existence before inserting roles/admin data.

| Entity | Count | Details |
|--------|-------|---------|
| `roles` | 6 | Canonical role names: `admin`, `operations_manager`, `market_agent`, `hub_staff`, `driver`, `restaurant` |
| `users` | 1 | Admin account: email/password from `AdminSeed:Email` / `AdminSeed:Password` config or `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` env vars; password is bcrypt-hashed |

There is no implemented seeder for markets, products, market products, hubs, vehicles, or
`system_config` in the current codebase.

### 5.4 Production Migration Policy

1. Migrations are **backward-compatible**: no column renames or type changes in the same release as the code that uses them. Two-phase approach: add column (deploy) → remove old column (next release).
2. **Never run manual SQL in production.** All schema changes go through EF Core migrations.
3. **Rollback**: revert the migration class and re-deploy. EF Core's `Down()` method is implemented for every migration. Emergency rollback: `dotnet ef database update {PreviousMigrationName}` run against the production database with controlled traffic drain.
4. **Partition creation**: not implemented for `price_snapshots`. If introduced later, use a
   dedicated migration/job and update this document alongside the code.
5. **Zero-downtime migrations**: treat as an operations policy. The current app relies on the
   hosted `AdminSeeder` to run migrations at startup; deployment orchestration should account
   for that startup behavior.

---

## 6. Redis Data Structures

Current implementation uses StackExchange.Redis only in the Pricing module for price-board
cache writes. SignalR is registered without the Redis backplane, and ASP.NET rate limiting
uses in-process fixed-window limiters in `Program.cs`.

| Key Pattern | Data Structure | Content | TTL | Invalidation Trigger | Notes |
|-------------|---------------|---------|-----|---------------------|-------|
| `price:{marketId}:{productId}` | Hash | Fields written today: `price`, `quantity`, `updated_at`, `updated_by` | 5 min absolute, reset on each write | Pricing `PriceCacheUpdatedEventHandler` writes through `RedisPriceBoardCache` | The `reserved` field is intentionally not written by Pricing. Live price board reads are DB-direct in v1 (`DbPriceBoardReader`), not Redis-backed. |

Planned or placeholder key helpers exist for route/reservation/analytics naming, but there is
no implemented Redis-backed route cache, analytics cache, session counter, SignalR backplane,
or Redis rate limiter in the current codebase.

### 6.1 Redis Key Expiry and Eviction Policy

- **Eviction policy:** operational deployment choice; no app-level enforcement.
- **Persistence:** operational deployment choice; no app-level enforcement.
- **Redis failure handling:** `RedisPriceBoardCache` issues `HSET` then `KeyExpire`. The current
  implementation does not wrap/suppress Redis errors inside that class. Reservation-related
  rollback behavior is not implemented.

### 6.2 Redis Key Space Summary

```
price:*                          → price/quantity cache (Hash), 5min TTL
reservation:*                    → helper exists, not wired to implemented order reservation flow
route:*                          → helper exists, planned only
analytics:*                      → helper exists, planned only
```

---

*End of FreshFlow Database Schema Document v1.0*

*Prepared by: Database Design Agent | Project: FFX Capstone 2026*
