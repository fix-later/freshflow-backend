# FreshFlow (FFX) — Database Schema

**Version:** 2.0 · **Reconciled with code:** 2026-08-22 (branch `dev-bao`, commit `bc5f1a4`)
**Engine:** PostgreSQL 16 · **ORM:** EF Core (Npgsql) · **Migrations applied:** 97

---

## ⚙️ Source of truth

> The schema is defined by EF Core, not by this document. In order of authority:
>
> | # | Artifact | What it settles |
> |---|---|---|
> | 1 | `src/FreshFlow.Infrastructure.Persistence/Migrations/AppDbContextModelSnapshot.cs` | The current model — every table, column, type, index |
> | 2 | `**/Persistence/Configurations/*Configuration.cs` | Exact physical column names, casing, defaults, check constraints, delete behaviour |
> | 3 | `Migrations/*.cs` | Raw SQL, seeds, and anything EF could not express |
>
> Companion documents in [`database/`](./database/README.md) are generated against artifact 1
> and reconciled on **2026-08-16**:
>
> | Doc | Content |
> |---|---|
> | [`03-database-schema.dbml`](./database/03-database-schema.dbml) | **Physical DBML** — 59 tables with columns, types, indexes, defaults, checks, 42 enforced FKs |
> | [`03-database-schema.logical.dbml`](./database/03-database-schema.logical.dbml) | Logical model — business attributes, enforced + application-level relationships |
> | [`03-database-schema.conceptual.dbml`](./database/03-database-schema.conceptual.dbml) + [`.conceptual.md`](./database/03-database-schema.conceptual.md) | Conceptual model, Chen ERD source |
> | [`03B-database-erd.md`](./database/03B-database-erd.md) | Mermaid crow's-foot ERD, per module |
> | [`03C-table-descriptions.md`](./database/03C-table-descriptions.md) | What each table is for, in business terms |
> | [`03D-entities-description.md`](./database/03D-entities-description.md) · [`03E-…`](./database/03E-conceptual-entities-description.md) | Entity/attribute descriptions |
>
> This file covers what those cannot: the **conventions and traps** you need before writing a
> query or a migration.

---

## 1. Conventions

| Rule | Detail |
|---|---|
| Primary keys | `UUID`, defaulted to `gen_random_uuid()`. No sequential integer keys anywhere. |
| Timestamps | Stored UTC as `timestamptz`. Business dates are **`Asia/Ho_Chi_Minh`** and converted at the handler boundary (`Analytics.Application/Common/VietnamTime.cs`). |
| `date` columns | Already business dates (e.g. `batch_date`) — do **not** timezone-convert them. |
| Soft delete | Most mutable tables carry `deleted_at TIMESTAMPTZ`. **Every query and every cross-module seam must filter it.** |
| Money | `numeric` — never floating point. |
| DbContext | One shared `AppDbContext`, no `DbSet<T>` properties; configurations are auto-discovered with `ApplyConfigurationsFromAssembly()` across module Infrastructure assemblies. |

### 1.1 Tables *without* soft delete

Filtering `deleted_at` on these fails at runtime:

- `order_items` — `OrderItemConfiguration` calls `builder.Ignore(i => i.DeletedAt)`
- `price_snapshots` — append-only
- `refresh_tokens` — append-only

### 1.2 ⚠️ Column casing is not consistent — never guess

Two conventions coexist, and some tables **mix both within a single table**:

| Table family | Convention |
|---|---|
| `procurement_*`, `market_session*`, `price_snapshots`, `hub_*`, `deliveries`, `delivery_routes`, `route_plans`, `vehicles` | fully `snake_case` |
| `orders`, `order_items` | **mixed** — `"Id"`, `"RestaurantId"`, `"Status"`, `"CreatedAt"`, `"Quantity"` are quoted PascalCase; `deleted_at`, `confirmed_receipt_at` are snake_case |
| `market_products` | **mixed** — `"Id"`, `"ProductId"`, `"MarketId"` PascalCase; `"deleted_at"` snake_case |
| `products`, `product_categories`, `markets` | PascalCase **including** `"DeletedAt"` |
| `restaurants`, `delivery_addresses` | **mixed** — `"Id"`, `"Name"`, `"RestaurantId"`, `"IsDefault"`, `"DeletedAt"` PascalCase; `status` snake_case with lowercase values (`'active'`) |

A wrong quoted identifier fails at **runtime**, not at build. Confirm against the module's
`*Configuration.cs`, or copy an existing seam.

### 1.3 Enum-ish string values are not consistent either

| Column | Casing | Values |
|---|---|---|
| `orders."Status"` | PascalCase | `Draft`, `Confirmed`, `Batched`, `PickedUp`, `AtHub`, `Delivering`, `Delivered`, `Cancelled` |
| `deliveries.status` | lowercase | `pending`, `arrived`, `delivered`, `failed` |
| `hub_*.status` | SCREAMING_CASE | `PENDING`, `ARRIVED_AT_HUB`, `PENDING_CHECKOUT`, `CHECKED_OUT`, `SORTED`, `OPEN`, `ACKNOWLEDGED` |
| `cross_dock_transfers.status` | lowercase | `pending`, `in_progress`, `completed` |
| `restaurants.status` | lowercase | `pending`, `active`, `suspended` |

The full list is in [`enums.md`](./enums.md); the binding definition is the entity's constants
or enum type.

---

## 2. Table Inventory — 59 tables

Grouped by owning module. The owning module is the only one that writes; everyone else reads
through a keyless Row seam (§4).

### Auth — 9

`users` · `roles` · `refresh_tokens` · `password_reset_tokens` · `verification_codes` ·
`restaurants` · `delivery_addresses` · `driver_profiles` · `user_market_assignments`

### Catalog — 5

`products` · `product_categories` · `units_of_measurement` · `markets` · `packing_codes`

### Pricing — 4

`market_products` · `price_snapshots` · `tags` · `market_product_tags`

### Orders — 12

`orders` · `order_items` · `order_issues` · `order_claims` · `scheduled_orders` ·
`scheduled_order_items` · `restaurant_credit` · `credit_transactions` · `credit_statements` ·
`credit_statement_lines` · `restaurant_favorites` · `operational_settings`

### Procurement — 7

`procurement_batches` · `procurement_batch_items` · `procurement_batch_orders` ·
`procurement_exceptions` · `market_sessions` · `market_session_agents` ·
`market_session_vehicles`

### Hub — 10

`hubs` · `hub_inbound_events` · `hub_outbound_events` · `hub_inventory` ·
`hub_discrepancies` · `hub_sorting_progress` · `hub_staff_assignments` ·
`hub_driver_assignments` · `hub_handover_events` · `cross_dock_transfers`

### Logistics — 6

`deliveries` · `delivery_issues` · `delivery_routes` · `route_plans` · `route_matrix_cache` ·
`vehicles`

### Invoicing — 2

`invoices` · `invoice_lines`

### Notifications — 2

`notifications` · `notification_devices`

### Platform (owned by the API host) — 2

`assistant_conversations` · `audit_logs`

> **Analytics owns zero tables.** It is a read-only module built entirely on keyless Row seams.

---

## 3. Things the older revision of this document got wrong

Corrected here so they are not re-planned:

| Claim in v1.0 | Reality |
|---|---|
| `price_snapshots` is monthly **range-partitioned** with a `PartitionMaintenanceJob` | It is a **plain table** with ordinary indexes. Neither the partitioning nor the job exists. |
| `payments` / `refunds` tables | Never built. Payment is a **B2B credit / công nợ** model: `restaurant_credit` + `credit_transactions` + `credit_statements`. Refunds are credit adjustments, not gateway refunds. |
| `order_groups` table | The concept exists, the table does not — it is `procurement_batches` (and the API path is `/admin/order-groups`). |
| `system_config` table | Implemented as `operational_settings`, a singleton row owned by Orders. `pricing_settings` was dropped in `20260813074950_DropPricingSettings`. |
| `analytics_aggregations`, `export_jobs` tables | Never built. Analytics computes on read. |
| Redis soft stock reservations + 5-minute reconciliation job | Not implemented. Stock is checked against PostgreSQL at confirm time. |

---

## 4. Cross-Module Reads: the keyless Row seam

No module's `Domain` or `Application` may reference another module's projects. To read another
module's table, define a **keyless row** mapped with `ToSqlQuery` — never `ToTable`, which
would collide with the owning module's model. All three files live in
`{Module}.Infrastructure/CrossModule/`:

```
XxxRow.cs               ← POCO, init-only props, PascalCase
XxxRowConfiguration.cs  ← HasNoKey() + ToSqlQuery("""SELECT … FROM other_table WHERE deleted_at IS NULL""")
XxxReader.cs            ← db.Set<XxxRow>().AsNoTracking() + LINQ
```

Two consequences worth knowing before planning a cross-module read:

- You cannot use another module's enum — re-declare the values locally as string constants.
- `ToSqlQuery` does **not** execute on the EF InMemory provider, so a seam is only actually
  proven by an **integration test** against real PostgreSQL.

Prefer extending an existing seam over adding a second one against the same table.

> New keyless Rows **do** enter `AppDbContextModelSnapshot`. `DesignTimeDbContextFactory.cs`
> calls `ForceLoadModuleAssemblies()` — every module Infrastructure assembly must be listed
> there, or the snapshot silently drifts and another module's next migration erases your rows.

---

## 5. SQL Policy

The repository contains **zero `FromSqlRaw`**. Keep it that way. Ladder, in order:

1. **LINQ over a keyless Row** — the default; covers nearly everything.
2. Aggregate inside a **static** `ToSqlQuery` (no parameters) — for functions LINQ cannot
   translate (e.g. `stddev_samp`, see `PriceSnapshotRowConfiguration`). Apply parameterized
   filters outside, in LINQ.
3. `FromSql($"")` (FormattableString) — last resort.

**Forbidden:** `FromSqlRaw`, string interpolation/concatenation/`string.Format` into SQL, and
any `ORDER BY {input}`. Client-chosen sort/group columns go through an allow-list `switch`
mapped to LINQ expressions.

---

## 6. Redis

Redis is used for **one** thing: the Pricing board cache. It is **not** a SignalR backplane and
**not** a stock-reservation store.

| Key | Type | TTL | Written by |
|---|---|---|---|
| `price:{marketId}:{productId}` | Hash — fields `price`, `quantity`, `updated_at`, `updated_by` | 5 min absolute, reset on every write | `Pricing.Infrastructure/Cache/RedisPriceBoardCache.cs` |

A cache miss falls back to PostgreSQL, so the API stays correct if Redis is down.

---

## 7. Migrations

```bash
# add
dotnet ef migrations add <Name> \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API

# apply
dotnet ef database update \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API
```

97 migrations are applied, from `20260531173716_InitAuth` through
`20260816165433_AddOrderClaimProofImageUrl`. The API applies pending migrations automatically
on startup, seeds the six roles, and seeds the default admin account — running
`dotnet ef database update` by hand is not part of the normal dev loop.

> ⚠️ `appsettings.json`'s `DefaultConnection` points at a **production** database. Always
> confirm the target before running a destructive migration.

---

## Changelog

| Version | Date | Change |
|---|---|---|
| 2.0 | 2026-08-22 | Rewritten as the conventions + inventory document. Hand-written DDL removed (it had drifted to 37 tables, six of which never existed); column-level truth now lives in `database/03-database-schema.dbml`. Added the casing traps, the seam rules, the SQL policy, the real Redis usage, and a table of corrected v1.0 claims. |
| 1.0 | 2026-05-09 | Original pre-implementation DDL design. |
