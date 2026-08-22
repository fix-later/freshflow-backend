# FreshFlow (FFX) — API Design Document

**Version:** 2.0 · **Reconciled with code:** 2026-08-22 (branch `dev-bao`, commit `bc5f1a4`)
**Project:** FreshFlow — Intermediary Platform for Food Procurement and Logistics Optimization
**Status:** As-built. This document describes the API that exists in the repository today.

---

## ⚙️ Source of truth

> The binding contract is the code: the controllers under `src/FreshFlow.API/Controllers/`,
> the SignalR hub mappings in `src/FreshFlow.API/Program.cs`, and the generated OpenAPI
> document. This file is the **navigable index** of that surface — every route below was
> extracted from the controllers, not from a design draft.
>
> | Artifact | Where |
> |---|---|
> | Live OpenAPI JSON | `GET /swagger/v1/swagger.json` |
> | Swagger UI | `/swagger` |
> | Scalar API reference | `/scalar/v1` |
> | Request/response DTOs | `{Module}.Application/**/*Dto.cs`, `*Request.cs` |
> | Validation rules | FluentValidation validators co-located with each command/query |
> | Business rules behind the endpoints | [`07-business-rules.md`](./07-business-rules.md) |
>
> Per-field request/response shapes are **not duplicated here** — they drift the moment a DTO
> changes. Read them from Swagger/Scalar.

**Surface as of this revision:** **223 endpoints** across **31 controllers**, plus **4 SignalR hubs**.

**Modules with an HTTP surface:** Auth, Catalog, Pricing, Orders, Procurement, Logistics,
Hub, Notifications, Analytics, Invoicing — plus the AI Assistant, which lives in the API host
rather than in a module.

---

## Table of Contents

1. [API Conventions](#1-api-conventions)
2. [Endpoint Inventory](#2-endpoint-inventory)
3. [SignalR Hubs](#3-signalr-hubs)
4. [Error Codes and HTTP Mapping](#4-error-codes-and-http-mapping)
5. [RBAC Summary](#5-rbac-summary)
6. [Rate Limiting](#6-rate-limiting)
7. [Media Uploads](#7-media-uploads)

---

## 1. API Conventions

### 1.1 Base URL and versioning

```
{host}/api/v1
```

URL path versioning. Breaking changes would ship under `/api/v2`; non-breaking additions go
into `v1` without a bump. There is currently only `v1`.

### 1.2 Response envelope

Built by `src/FreshFlow.API/ApiResponse.cs` and `Extensions/ErrorExtensions.cs`.

**Success:**

```json
{ "success": true, "data": { } }
```

**Success, cursor-paginated** (`ApiResponse.OkPaged`):

```json
{ "success": true, "data": [ ], "meta": { "pageSize": 20, "nextCursor": "…" } }
```

**Success, no payload** (`ApiResponse.OkEmpty`):

```json
{ "success": true, "data": null }
```

**Error:**

```json
{ "success": false, "error": { "code": "VALIDATION_ERROR", "message": "…" } }
```

`nextCursor` is `null` on the last page. Cursors are opaque — do not decode or construct them.
Some list endpoints return offset metadata (`page`, `pageSize`, `total`) instead; the endpoint's
Swagger schema is authoritative on which one it uses.

### 1.3 HTTP status codes

Status is derived from `Error.Code` by `ErrorExtensions.ToActionResult()` — see
[§4](#4-error-codes-and-http-mapping). Summary:

| Code | When |
|---|---|
| 200 / 201 / 204 | Success |
| 400 | `VALIDATION_ERROR`, malformed body, OTP-format errors |
| 401 | Missing/expired/invalid token, bad credentials |
| 403 | Valid token, insufficient role or resource ownership |
| 404 | Any `*_NOT_FOUND` code; soft-deleted rows read as absent |
| 409 | Duplicates, state conflicts, optimistic-concurrency conflicts |
| 422 | Business-rule violation (the largest bucket) |
| 423 | `ACCOUNT_LOCKED` |
| 429 | Rate limit exceeded |
| 500 | Unhandled exception, or an **unregistered error code** |

> Unregistered error codes fall through to **500**. Always reuse an existing code rather than
> inventing one.

### 1.4 Dates and times

ISO 8601 in, ISO 8601 out. Stored as UTC (`timestamptz`). Business dates are
**`Asia/Ho_Chi_Minh`** and are converted at the handler boundary
(`Analytics.Application/Common/VietnamTime.cs`). `date` columns that already hold a business
date (e.g. `batch_date`) are **not** converted.

### 1.5 Field naming

Request and response bodies use **camelCase**. Database column names are never exposed.

### 1.6 Soft deletes

A soft-deleted row (`deleted_at IS NOT NULL`) reads as **404**. Not every table has soft
delete — `order_items`, `price_snapshots` and `refresh_tokens` do not.

### 1.7 Authentication

```
Authorization: Bearer <accessToken>
```

JWT access tokens are stateless (15 min). Refresh tokens (7 days) are stored hashed in
`refresh_tokens` and rotate on every use; reuse of a rotated token invalidates the family
(`REFRESH_TOKEN_REUSE`).

SignalR connections pass the token in the query string during negotiate:
`?access_token=<accessToken>`.

Public (no token): `register`, `login`, `refresh`, `forgot-password`, `reset-password`,
`verify/request`, `verify`, `GET /categories`, `GET /categories/{id}`, `GET /markets`,
`GET /markets/{id}`, `GET /markets/{marketId}/products`, `GET /orders/ordering-window`.

**Roles.** Exactly six exist, seeded by `20260606152229_AddRolesTable.cs`:
`admin`, `operations_manager`, `market_agent`, `hub_staff`, `driver`, `restaurant`.
Each user has one global role via `users.role_id`. `kiosk_staff` is accepted only as a legacy
request alias for `market_agent` on Admin user creation.

> `[Authorize(Roles = …)]` with a name that is not in that list fails **silently** — the build
> is green and the endpoint returns 403 forever. Verify role names against the seed.

**Phone OTP** is not implemented: password reset and verification are email-only, and a
`PHONE` channel returns `CHANNEL_NOT_SUPPORTED`.

---

## 2. Endpoint Inventory

Extracted from the controllers. Where the **Roles** column says `authenticated`, the action
inherits the controller-level `[Authorize]` with no role restriction; `public` means
`[AllowAnonymous]`. ASP.NET stacks `[Authorize]` attributes as **AND**, so an action-level role
list narrows the controller-level one, never widens it.

Route parameters are shown without their `:guid` constraint.

### A1. Authentication

**Controller:** `AuthController.cs` · **Module:** Auth · **Controller-level roles:** none (per-action only)

Public auth flows + session management. Rate-limited by the `auth` policy (10 req/min/IP).

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | public |  |
| `POST` | `/api/v1/auth/login` | public |  |
| `POST` | `/api/v1/auth/refresh` | public |  |
| `POST` | `/api/v1/auth/logout` | authenticated |  |
| `POST` | `/api/v1/auth/forgot-password` | public |  |
| `POST` | `/api/v1/auth/reset-password` | public |  |
| `POST` | `/api/v1/auth/verify/request` | public |  |
| `POST` | `/api/v1/auth/verify` | public |  |
| `POST` | `/api/v1/auth/change-password` | authenticated |  |

### A2. User Profile

**Controller:** `ProfileController.cs` · **Module:** Auth · **Controller-level roles:** any authenticated user

The authenticated user's own profile, any role.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/profile/me` | any authenticated user | returns the authenticated user's personal profile. |
| `PUT` | `/api/v1/profile/me` | any authenticated user | updates the authenticated user's personal profile. |
| `POST` | `/api/v1/profile/me/avatar/upload-signature` | any authenticated user | signs a Cloudinary avatar upload. |

### A3. Admin & Operations

**Controller:** `AdminController.cs` · **Module:** Auth / Procurement / Orders · **Controller-level roles:** any authenticated user

User provisioning, restaurant approval, credit control, operational settings, phiên chợ (market sessions), order groups (procurement batches), audit logs, market assignments.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/admin/users` | `admin` |  |
| `GET` | `/api/v1/admin/users` | `admin` |  |
| `PATCH` | `/api/v1/admin/users/{userId}/activate` | `admin` |  |
| `POST` | `/api/v1/admin/users/{userId}/unlock` | `admin` |  |
| `GET` | `/api/v1/admin/roles` | `admin` |  |
| `PATCH` | `/api/v1/admin/users/{userId}/role` | `admin` |  |
| `GET` | `/api/v1/admin/restaurants/{restaurantId}/profile` | `admin` | Returns the full profile (incl. tax/invoice fields) of any restaura |
| `PATCH` | `/api/v1/admin/restaurants/{restaurantId}/approve` | `admin` |  |
| `PATCH` | `/api/v1/admin/restaurants/{restaurantId}/suspend` | `admin` |  |
| `PATCH` | `/api/v1/admin/restaurants/{restaurantId}/reactivate` | `admin` | Restores a suspended restaurant to active. Only accepts restau |
| `POST` | `/api/v1/admin/restaurants/{restaurantId}/credit/settle` | `admin` | Records a debt payment against a restaurant's outstanding cr |
| `PUT` | `/api/v1/admin/restaurants/{restaurantId}/credit/limit` | `admin` |  |
| `GET` | `/api/v1/admin/operational-settings` | `admin` |  |
| `PUT` | `/api/v1/admin/operational-settings` | `admin` |  |
| `GET` | `/api/v1/admin/market-sessions` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/admin/market-sessions/{id}` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/admin/market-sessions/{id}/tracking` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/admin/market-sessions/{id}/resource-options` | `admin`, `operations_manager` |  |
| `PUT` | `/api/v1/admin/market-sessions/{id}/resources` | `admin` |  |
| `PUT` | `/api/v1/admin/market-sessions/{id}` | `admin` |  |
| `POST` | `/api/v1/admin/market-sessions/{id}/open` | `admin` |  |
| `POST` | `/api/v1/admin/market-sessions/{id}/close` | `admin` |  |
| `GET` | `/api/v1/admin/order-groups` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/admin/order-groups/{batchId}` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/admin/order-groups/progress` | `admin` |  |
| `POST` | `/api/v1/admin/order-groups/auto-batch` | `admin` |  |
| `POST` | `/api/v1/admin/order-groups/reset` | `admin` |  |
| `POST` | `/api/v1/admin/order-groups/{batchId}/manifest` | `admin` |  |
| `PUT` | `/api/v1/admin/batches/{batchId}/item-assignments` | `admin` |  |
| `POST` | `/api/v1/admin/order-groups/{batchId}/cancel` | `admin` | cancels the session and every order it covers. Only allowed before th |
| `GET` | `/api/v1/admin/audit-logs` | `admin` | filter by actor/action/entity/time. |
| `GET` | `/api/v1/admin/users/{userId}/market-assignments` | `admin`, `operations_manager` | Returns the current market assignments for the specified user. |
| `PUT` | `/api/v1/admin/users/{userId}/market-assignments` | `admin`, `operations_manager` | Replaces all market assignments for the specified user (must be a ma |

### A4. Restaurant Profile & Delivery Addresses

**Controller:** `RestaurantProfileController.cs` · **Module:** Auth · **Controller-level roles:** `restaurant`

Self-service profile, tax/invoice profile and delivery addresses for the logged-in restaurant.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/restaurants/me/approval-status` | `restaurant` | returns the restaurant's current approval status. |
| `PUT` | `/api/v1/restaurants/me/tax-profile` | `restaurant` | updates the authenticated restaurant's invoice tax profile. |
| `GET` | `/api/v1/restaurants/me/profile` | `restaurant` | returns the authenticated restaurant's profile. |
| `PUT` | `/api/v1/restaurants/me/profile` | `restaurant` | updates the authenticated restaurant's profile. |
| `POST` | `/api/v1/restaurants/me/business-license/upload-signature` | `restaurant` | signs a Cloudinary business-license upload. |
| `GET` | `/api/v1/restaurants/me/delivery-addresses` | `restaurant` | lists all active delivery addresses. |
| `POST` | `/api/v1/restaurants/me/delivery-addresses` | `restaurant` | adds a delivery address. |
| `PUT` | `/api/v1/restaurants/me/delivery-addresses/{id}` | `restaurant` | updates a delivery address. |
| `DELETE` | `/api/v1/restaurants/me/delivery-addresses/{id}` | `restaurant` | soft-deletes a delivery address. |

### A5. Restaurant Credit (B2B công nợ)

**Controller:** `RestaurantCreditController.cs` · **Module:** Orders · **Controller-level roles:** `admin`, `restaurant`

Credit balance, ledger and monthly statements. A restaurant may only read its own; admin may read any.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/restaurants/{restaurantId}/credit` | `admin`, `restaurant` |  |
| `GET` | `/api/v1/restaurants/{restaurantId}/credit/transactions` | `admin`, `restaurant` | Returns the cursor-paginated credit transaction (balance ledg |
| `POST` | `/api/v1/restaurants/{restaurantId}/credit/statements/generate` | `admin`, `restaurant` | Generates the immutable credit statement for the give |
| `GET` | `/api/v1/restaurants/{restaurantId}/credit/statements/{statementId}` | `admin`, `restaurant` | Returns a single credit statement (with line item |
| `GET` | `/api/v1/restaurants/{restaurantId}/credit/statements/{statementId}/pdf` | `admin`, `restaurant` | Renders the same statement as <see cref="GetS |
| `GET` | `/api/v1/restaurants/{restaurantId}/credit/statements` | `admin`, `restaurant` | Returns the cursor-paginated statement history for a restaurant |

### A6. Restaurant Favorites

**Controller:** `RestaurantFavoritesController.cs` · **Module:** Orders · **Controller-level roles:** `restaurant`

Favorite market-product listings. All operations are idempotent.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/restaurants/me/favorites` | `restaurant` | lists the restaurant's favorites, enriched. |
| `POST` | `/api/v1/restaurants/me/favorites` | `restaurant` | adds a favorite (idempotent). |
| `DELETE` | `/api/v1/restaurants/me/favorites/{marketProductId}` | `restaurant` | removes a favorite (idempotent — already-removed is also a 2 |

### A7. Catalog — Categories

**Controller:** `CategoriesController.cs` · **Module:** Catalog · **Controller-level roles:** any authenticated user

Product category tree.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/categories` | public | public (guests included); active-only by default. |
| `GET` | `/api/v1/categories/{id}` | public | public (guests included). |
| `POST` | `/api/v1/categories` | `admin` | Admin only. |
| `POST` | `/api/v1/categories/image/upload-signature` | `admin` | Admin only. |
| `PUT` | `/api/v1/categories/{id}` | `admin` | Admin only. |
| `PATCH` | `/api/v1/categories/{id}/deactivate` | `admin` | Admin only. |
| `PATCH` | `/api/v1/categories/{id}/activate` | `admin` | Admin only. |

### A8. Catalog — Units of Measurement

**Controller:** `UnitsController.cs` · **Module:** Catalog · **Controller-level roles:** any authenticated user

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/units` | any authenticated user | any authenticated user; active-only by default. |
| `GET` | `/api/v1/units/{id}` | any authenticated user | any authenticated user. |
| `POST` | `/api/v1/units` | `admin` | Admin only. |
| `PUT` | `/api/v1/units/{id}` | `admin` | Admin only. |
| `PATCH` | `/api/v1/units/{id}/deactivate` | `admin` | Admin only. |

### A9. Catalog — Packing Codes

**Controller:** `PackingCodesController.cs` · **Module:** Catalog · **Controller-level roles:** `admin`

Box/packing specs used by the shipping estimate.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/catalog/packing-codes` | `admin` |  |
| `GET` | `/api/v1/catalog/packing-codes` | `admin` |  |
| `GET` | `/api/v1/catalog/packing-codes/{id}` | `admin` |  |
| `PUT` | `/api/v1/catalog/packing-codes/{id}` | `admin` |  |
| `PATCH` | `/api/v1/catalog/packing-codes/{id}/deactivate` | `admin` |  |

### A10. Catalog — Products

**Controller:** `ProductsController.cs` · **Module:** Catalog · **Controller-level roles:** any authenticated user

Master product catalogue (not market-specific).

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/products` | `admin` | Admin only. Market Agents are explicitly blocked (403). |
| `POST` | `/api/v1/products/image/upload-signature` | `admin` | Admin only. Signs a Cloudinary product image upload. |
| `GET` | `/api/v1/products` | `admin`, `operations_manager`, `market_agent`, `hub_staff`, `restaurant` | Returns a paged list of products. |
| `GET` | `/api/v1/products/{id}` | `admin`, `operations_manager`, `market_agent`, `hub_staff`, `restaurant` | Returns a single product by ID. |
| `PUT` | `/api/v1/products/{id}` | `admin` | Admin only. |
| `PATCH` | `/api/v1/products/{id}/deactivate` | `admin` | Admin only. |

### A11. Catalog — Tags

**Controller:** `TagsController.cs` · **Module:** Pricing · **Controller-level roles:** any authenticated user

Tags assignable to market-product listings.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/tags` | any authenticated user | any authenticated user. |
| `POST` | `/api/v1/tags` | `admin` | admin only. |
| `PUT` | `/api/v1/tags/{id}` | `admin` | admin only. |
| `DELETE` | `/api/v1/tags/{id}` | `admin` | admin only (soft-delete + clears assignments). |

### A12. Markets & Market Products

**Controller:** `MarketsController.cs` · **Module:** Catalog / Pricing · **Controller-level roles:** any authenticated user

Markets plus the per-market product listings that carry live price and quantity.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/markets` | public | public (guests included); active-only by default. |
| `GET` | `/api/v1/markets/{id}` | public | public (guests included). |
| `POST` | `/api/v1/markets` | `admin` | Admin only. |
| `POST` | `/api/v1/markets/image/upload-signature` | `admin` | Admin only. |
| `PUT` | `/api/v1/markets/{id}` | `admin` | Admin only. |
| `PATCH` | `/api/v1/markets/{id}/deactivate` | `admin` | Admin only. |
| `DELETE` | `/api/v1/markets/{id}` | `admin` | Admin only (soft-delete). |
| `GET` | `/api/v1/markets/{marketId}/products` | public | Returns active products at a specific market with current price and stock. Curso |
| `POST` | `/api/v1/markets/{marketId}/products` | `admin` | Lists a catalog product at a market with an initial price and quantity. Admin o |
| `DELETE` | `/api/v1/markets/{marketId}/products/{productId}` | `admin` | Removes a product listing from a market. Admin only (soft-delete) |
| `GET` | `/api/v1/markets/{marketId}/products/{productId}/price-history` | any authenticated user | Returns the cursor-paginated price/quantity change his |
| `PATCH` | `/api/v1/markets/{marketId}/products/{productId}/price` | `admin`, `market_agent` | Updates the price and/or available quantity of a product at |
| `PATCH` | `/api/v1/markets/{marketId}/products/{productId}/quantity` | `admin`, `market_agent` | Sets the available procurement quantity of a product at a |
| `PUT` | `/api/v1/markets/{marketId}/products/{productId}/tags` | `admin`, `market_agent` | Replaces the tag assignment set of a product listing at this ma |

### A13. Pricing

**Controller:** `PricingController.cs` · **Module:** Pricing · **Controller-level roles:** any authenticated user

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/pricing/assigned-markets` | `market_agent` | Returns the list of markets the calling market agent is assigned to. |

### A14. Market Sessions (phiên chợ) — read

**Controller:** `MarketSessionsController.cs` · **Module:** Procurement · **Controller-level roles:** any authenticated user

Read-only session lookup for ordering clients. Admin write operations live in A3.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/market-sessions/availability` | `restaurant` |  |
| `GET` | `/api/v1/market-sessions` | any authenticated user |  |

### A15. Orders & Scheduled Orders

**Controller:** `OrdersController.cs` · **Module:** Orders · **Controller-level roles:** `admin`, `operations_manager`, `restaurant`

Rate-limited by the `orders` policy (30 req/min/user).

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/orders` | `admin`, `operations_manager`, `restaurant` | UC-ORD-12/20: lists orders/history with pagination and filters. |
| `GET` | `/api/v1/orders/history` | `admin`, `operations_manager`, `restaurant` | UC-ORD-20 alias over the same order-list filters. |
| `GET` | `/api/v1/orders/ordering-window` | public |  |
| `GET` | `/api/v1/orders/scheduled` | `admin`, `restaurant` | UC-ORD-10: lists recurring scheduled orders. |
| `GET` | `/api/v1/orders/scheduled/{scheduledOrderId}` | `admin`, `restaurant` | UC-ORD-10: recurring schedule detail. |
| `GET` | `/api/v1/orders/scheduled/{scheduledOrderId}/instances` | `admin`, `restaurant` | UC-ORD-11: lists generated concrete order instances. |
| `GET` | `/api/v1/orders/{orderId}` | `admin`, `operations_manager`, `restaurant` | UC-ORD-13: returns an order with line items. |
| `POST` | `/api/v1/orders` | `restaurant` | UC-ORD-01: creates a draft order with one or more line items. |
| `POST` | `/api/v1/orders/scheduled` | `restaurant` | UC-ORD-09: creates a recurring scheduled order. |
| `POST` | `/api/v1/orders/{orderId}/items` | `restaurant` | UC-ORD-02: adds an item to a draft order. |
| `PUT` | `/api/v1/orders/{orderId}/items/{itemId}` | `restaurant` | UC-ORD-03: updates a draft order item's quantity. |
| `DELETE` | `/api/v1/orders/{orderId}/items/{itemId}` | `restaurant` | UC-ORD-04: removes an item from a draft order. |
| `POST` | `/api/v1/orders/{orderId}/confirm` | `restaurant` | UC-ORD-06/07/08: confirms a draft order, checks B2B credit, locks item prices, a |
| `GET` | `/api/v1/orders/{orderId}/confirm-preview` | `restaurant` | ASSIST-E3: dry-run of confirm that surfaces every blocking issue (credit |
| `PATCH` | `/api/v1/orders/{orderId}/cancel` | `admin`, `restaurant` | UC-ORD-15: cancels a draft/confirmed order. |
| `PATCH` | `/api/v1/orders/{orderId}/items/{itemId}/actual-quantity` | `admin`, `operations_manager` | UC-ORD-16/17: records fulfilled quantity after shortage/ |
| `POST` | `/api/v1/orders/{orderId}/advance-status` | `admin`, `operations_manager` | ops bridge that advances a confirmed order through the pre-hub pipeline ( |
| `PATCH` | `/api/v1/orders/{orderId}/receipt` | `restaurant` | UC-ORD-18: confirms receipt of a delivered order. |
| `POST` | `/api/v1/orders/{orderId}/issues` | `restaurant` | UC-ORD-19: reports an issue for a delivered order. |
| `POST` | `/api/v1/orders/{orderId}/reorder` | `restaurant` | UC-ORD-21: creates a draft order from order history. |
| `PATCH` | `/api/v1/orders/scheduled/{scheduledOrderId}` | `admin`, `restaurant` | UC-ORD-10: updates a recurring schedule. |
| `PATCH` | `/api/v1/orders/scheduled/{scheduledOrderId}/cancel` | `admin`, `restaurant` | UC-ORD-10: cancels a recurring schedule. |

### A16. Order Claims

**Controller:** `ClaimsController.cs` · **Module:** Orders · **Controller-level roles:** `admin`, `operations_manager`, `restaurant`

Post-delivery compensation claims; approval credits the restaurant's công nợ.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/orders/{orderId}/claims` | `restaurant` |  |
| `POST` | `/api/v1/orders/{orderId}/claims/upload-signature` | `restaurant` |  |
| `PATCH` | `/api/v1/claims/{claimId}/approve` | `admin`, `operations_manager` |  |
| `PATCH` | `/api/v1/claims/{claimId}/reject` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/claims/{claimId}` | `admin`, `operations_manager`, `restaurant` |  |
| `GET` | `/api/v1/claims` | `admin`, `operations_manager`, `restaurant` |  |

### A17. VAT Invoices

**Controller:** `InvoicesController.cs` · **Module:** Invoicing · **Controller-level roles:** `admin`, `operations_manager`, `restaurant`

Invoices are issued automatically per completed delivery; this surface is read-only.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/invoices` | `admin`, `operations_manager`, `restaurant` | Lists VAT invoices. Admin/ops see all; a restaurant sees only its own. |
| `GET` | `/api/v1/invoices/summary` | `admin`, `operations_manager`, `restaurant` | Aggregates issued invoices for reconciliation; this is not a VAT invoice. |
| `GET` | `/api/v1/invoices/{invoiceId}` | `admin`, `operations_manager`, `restaurant` | Returns a single invoice with its lines and tax-authority lookup URL. |
| `GET` | `/api/v1/invoices/{invoiceId}/export` | `admin`, `operations_manager`, `restaurant` | Exports one issued invoice as its persisted structured XML document. |
| `GET` | `/api/v1/invoices/{invoiceId}/pdf` | `admin`, `operations_manager`, `restaurant` | Downloads a sandbox invoice as a clearly marked, non-legal PDF draft. |

### A18. Procurement — Market Agent tasks

**Controller:** `ProcurementController.cs` · **Module:** Procurement · **Controller-level roles:** `market_agent`

The market agent's working surface for a phiên chợ.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/procurement/tasks` | `market_agent` |  |
| `GET` | `/api/v1/procurement/tasks/{batchId}` | `market_agent` |  |
| `PATCH` | `/api/v1/procurement/tasks/{batchId}/purchase` | `market_agent` |  |
| `PATCH` | `/api/v1/procurement/tasks/{batchId}/handover` | `market_agent` |  |
| `POST` | `/api/v1/procurement/tasks/{batchId}/exceptions` | `market_agent` |  |
| `POST` | `/api/v1/procurement/tasks/{batchId}/exceptions/upload-signature` | `market_agent` |  |

### A19. Procurement — Batch Overview

**Controller:** `ProcurementBatchOverviewController.cs` · **Module:** Procurement · **Controller-level roles:** `admin`, `operations_manager`

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/procurement/batches/{batchId}/overview` | `admin`, `operations_manager` |  |

### A20. Hubs — CRUD

**Controller:** `HubsController.cs` · **Module:** Hub · **Controller-level roles:** `admin`, `operations_manager`

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/hubs` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{id}` | `admin`, `operations_manager` |  |
| `PATCH` | `/api/v1/hubs/{id}` | `admin`, `operations_manager` |  |
| `DELETE` | `/api/v1/hubs/{id}` | `admin`, `operations_manager` |  |

### A21. Hubs — Staff & Driver Assignments

**Controller:** `HubStaffAssignmentsController.cs` · **Module:** Hub · **Controller-level roles:** none (per-action only)

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/hubs/{hubId}/staff-assignments` | `admin`, `operations_manager` |  |
| `PUT` | `/api/v1/hubs/{hubId}/staff-assignments` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/driver-assignments` | `admin`, `operations_manager` |  |
| `PUT` | `/api/v1/hubs/{hubId}/driver-assignments` | `admin`, `operations_manager` | Replaces the drivers stationed at this hub. A driver may be on several hubs; this says where they work, not what they ar |
| `GET` | `/api/v1/hubs/assigned` | `hub_staff` |  |

### A22. Hub Operations — inbound, sorting, cross-dock, outbound

**Controller:** `HubInboundController.cs` · **Module:** Hub · **Controller-level roles:** `hub_staff`, `admin`, `operations_manager`

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/hubs/{hubId}/inbound` | `hub_staff`, `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/scan` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/pending-inbound` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/procurement-plan` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/orders-by-restaurant` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/inbound` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/inbound/{inboundId}/labels` | `hub_staff`, `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/{hubId}/inbound/{inboundId}/discrepancy` | `hub_staff`, `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/{hubId}/inbound/{inboundId}/discrepancy/upload-signature` | `hub_staff` |  |
| `GET` | `/api/v1/hubs/{hubId}/discrepancies` | `hub_staff`, `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/{hubId}/discrepancies/{discrepancyId}/acknowledge` | `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/{hubId}/cross-dock` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/cross-dock` | `hub_staff`, `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/{hubId}/outbound` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/outbound` | `hub_staff`, `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/{hubId}/sorting` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/sorting-progress` | `hub_staff`, `admin`, `operations_manager` |  |

### A23. Hub — Driver Handover

**Controller:** `HubHandoverController.cs` · **Module:** Hub · **Controller-level roles:** none (per-action only)

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/hubs/{hubId}/handover` | `hub_staff`, `admin`, `operations_manager` |  |
| `POST` | `/api/v1/hubs/{hubId}/handover/{id}/checkout` | `driver` |  |
| `GET` | `/api/v1/hubs/{hubId}/handovers` | `hub_staff`, `admin`, `operations_manager` |  |
| `GET` | `/api/v1/hubs/{hubId}/drivers/eligible` | `hub_staff`, `admin`, `operations_manager` |  |

### A24. Logistics — Vehicles

**Controller:** `VehiclesController.cs` · **Module:** Logistics · **Controller-level roles:** `admin`, `operations_manager`, `hub_staff`

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/logistics/vehicles` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/logistics/vehicles` | `admin`, `operations_manager`, `hub_staff` |  |
| `GET` | `/api/v1/logistics/vehicles/{id}` | `admin`, `operations_manager`, `hub_staff` |  |
| `PUT` | `/api/v1/logistics/vehicles/{id}` | `admin`, `operations_manager` |  |
| `PUT` | `/api/v1/logistics/vehicles/{id}/hub` | `admin`, `operations_manager` |  |
| `DELETE` | `/api/v1/logistics/vehicles/{id}` | `admin`, `operations_manager` |  |

### A25. Logistics — Routes & Route Plans

**Controller:** `RoutesController.cs` · **Module:** Logistics · **Controller-level roles:** none (per-action only)

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/logistics/routes/calculate` | `admin`, `operations_manager` |  |
| `POST` | `/api/v1/logistics/routes/plan` | `admin`, `operations_manager` |  |
| `POST` | `/api/v1/logistics/routes/plans/{planId}/approve` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/logistics/routes/plans/{planId}` | `admin`, `operations_manager` |  |
| `POST` | `/api/v1/logistics/routes/{id}/select` | `admin`, `operations_manager` |  |
| `POST` | `/api/v1/logistics/routes/{id}/optimize` | `admin`, `operations_manager` |  |
| `POST` | `/api/v1/logistics/routes/{id}/review` | `admin`, `operations_manager` |  |
| `POST` | `/api/v1/logistics/routes/{id}/assign-vehicle` | `admin`, `operations_manager`, `hub_staff` |  |
| `GET` | `/api/v1/logistics/routes` | `admin`, `operations_manager`, `hub_staff` |  |
| `GET` | `/api/v1/logistics/routes/suggestions` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/logistics/routes/{routeId}/eligibility` | `admin`, `operations_manager`, `hub_staff` |  |
| `GET` | `/api/v1/logistics/routes/{id}` | `admin`, `operations_manager`, `hub_staff` |  |
| `GET` | `/api/v1/logistics/routes/{routeId}/deliveries` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/logistics/routes/{id}/loading-manifest` | `admin`, `operations_manager`, `hub_staff`, `driver` |  |

### A26. Logistics — Shipping Estimate

**Controller:** `ShippingController.cs` · **Module:** Logistics · **Controller-level roles:** `admin`, `operations_manager`

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/logistics/shipping/orders/{orderId}/estimate` | `admin`, `operations_manager` |  |

### A27. Driver — last-mile execution

**Controller:** `DriverController.cs` · **Module:** Logistics · **Controller-level roles:** `driver`

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/driver/routes/today` | `driver` |  |
| `GET` | `/api/v1/driver/routes` | `driver` |  |
| `POST` | `/api/v1/driver/routes/{routeId}/start` | `driver` |  |
| `POST` | `/api/v1/driver/routes/{routeId}/reorder` | `driver` |  |
| `POST` | `/api/v1/driver/routes/{routeId}/confirm-pickup` | `driver` |  |
| `POST` | `/api/v1/driver/deliveries/{deliveryId}/proof-of-delivery/upload-signature` | `driver` |  |
| `PUT` | `/api/v1/driver/deliveries/{deliveryId}/proof-of-delivery` | `driver` |  |
| `PATCH` | `/api/v1/driver/deliveries/{deliveryId}/status` | `driver` |  |
| `POST` | `/api/v1/driver/deliveries/{deliveryId}/issues` | `driver` |  |

### A28. Notifications

**Controller:** `NotificationController.cs` · **Module:** Notifications · **Controller-level roles:** any authenticated user

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/notifications` | any authenticated user |  |
| `PATCH` | `/api/v1/notifications/{id}/read` | any authenticated user |  |

### A29. Notification Devices (push tokens)

**Controller:** `NotificationDeviceController.cs` · **Module:** Notifications · **Controller-level roles:** any authenticated user

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/notifications/devices` | any authenticated user |  |
| `DELETE` | `/api/v1/notifications/devices` | any authenticated user |  |

### A30. Analytics

**Controller:** `AnalyticsController.cs` · **Module:** Analytics · **Controller-level roles:** any authenticated user

Read-only module; no tables of its own.

| Method | Path | Roles | Notes |
|---|---|---|---|
| `GET` | `/api/v1/analytics/overview` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/analytics/price-trends` | `admin`, `operations_manager`, `restaurant` |  |
| `GET` | `/api/v1/analytics/order-metrics` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/analytics/procurement-metrics` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/analytics/hub-throughput` | `admin`, `operations_manager`, `hub_staff` |  |
| `GET` | `/api/v1/analytics/delivery-performance` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/analytics/demand-heatmap` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/analytics/demand-heatmap/time-distribution` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/analytics/recent-activities` | `admin`, `operations_manager` |  |
| `GET` | `/api/v1/analytics/export` | `admin`, `operations_manager` |  |

### A31. AI Shopping Assistant

**Controller:** `AssistantController.cs` · **Module:** API host (not a module) · **Controller-level roles:** `restaurant`

Rate-limited by the `assistant` policy (15 req/min/user).

| Method | Path | Roles | Notes |
|---|---|---|---|
| `POST` | `/api/v1/assistant/chat` | `restaurant` | one conversational turn against the shopping assistant. |

---

## 3. SignalR Hubs

Registered with a plain `AddSignalR()` — **in-memory, no Redis backplane**. The deployment is
single-instance; Redis is used for the Pricing board cache only, not for scale-out. Each hub
lives in its owning module's `Infrastructure/Realtime/` folder; there is no
`FreshFlow.API/SignalR/`.

| Hub | Route | Class | Authorization |
|---|---|---|---|
| `PricingHub` | `/hubs/pricing` | `Pricing.Infrastructure/Realtime/` | `[Authorize]` (any role) |
| `OrderHub` | `/hubs/orders` | `Orders.Infrastructure/Realtime/` | `admin`, `operations_manager`, `restaurant` |
| `DeliveryHub` | `/hubs/delivery` | `Logistics.Infrastructure/Realtime/` | `admin`, `operations_manager`, `restaurant` |
| `NotificationHub` | `/hubs/notifications` | `Notifications.Infrastructure/Realtime/` | `[Authorize]` (any role) |

### 3.1 Groups

| Group | Joined by | Used by |
|---|---|---|
| `market:{marketId}` | client call to `JoinMarketAsync` on `PricingHub`, server-checked against the caller's market assignments | `PricingHub` |
| `restaurant:{restaurantId}` | automatically on connect, resolved from the JWT | `OrderHub`, `DeliveryHub` |
| `user:{userId}` | automatically on connect | `NotificationHub` |
| `admin:orders` | automatically on connect for `admin` / `operations_manager` | `OrderHub` |
| `admin:delivery` | automatically on connect for `admin` / `operations_manager` | `DeliveryHub` |

`PricingHub` is the only hub with client-callable methods: `JoinMarketAsync(marketId)` and
`LeaveMarketAsync(marketId)`. The others join their groups in `OnConnectedAsync`.

### 3.2 Server → client events

| Event | Hub | Sent to |
|---|---|---|
| `PriceUpdated` | `PricingHub` | `market:{marketId}` |
| `OrderStatusChanged` | `OrderHub` | `restaurant:{restaurantId}` and `admin:orders` |
| `DeliveryStarted` | `DeliveryHub` | `restaurant:{restaurantId}` and `admin:delivery` |
| `DeliveryStopUpdated` | `DeliveryHub` | `restaurant:{restaurantId}` and `admin:delivery` |
| `NotificationCreated` | `NotificationHub` | `user:{userId}` |

Broadcasts originate in `{Module}.Infrastructure/Realtime/*BroadcastService.cs`, behind
interfaces declared in `{Module}.Application/Abstractions/`.

---

## 4. Error Codes and HTTP Mapping

`ErrorExtensions.ToActionResult()` maps an `Error.Code` string to a status code by allow-list,
in this order:

| Rule | Status |
|---|---|
| Code ends with `_NOT_FOUND`, or is `SCAN_NO_MATCH` | 404 |
| Duplicate / already-in-state / state-machine conflicts (`*_ALREADY_*`, `BATCH_NOT_*`, `ROUTE_PLAN_*`, `MARKET_SESSION_*` conflicts, `PLATE_NUMBER_DUPLICATE`, …) | 409 |
| `UNAUTHORIZED`, `INVALID_CREDENTIALS`, `INVALID_CURRENT_PASSWORD`, `TOKEN_INVALID`, `REFRESH_TOKEN_EXPIRED`, `REFRESH_TOKEN_REVOKED` | 401 |
| `FORBIDDEN`, `MARKET_ACCESS_DENIED`, `HUB_ACCESS_DENIED`, `ITEM_NOT_ASSIGNED_TO_AGENT` | 403 |
| `OPTIMISTIC_CONCURRENCY_CONFLICT`, `SERIALIZATION_CONFLICT`, `STOCK_RESERVATION_CONFLICT` | 409 |
| `VALIDATION_ERROR`, `INVALID_ROLE`, `WEAK_PASSWORD`, `VEHICLE_HUB_UNASSIGNED`, `VEHICLE_HUB_MISMATCH`, `RESET_OTP_INVALID`, `OTP_INVALID` | 400 |
| `ACCOUNT_LOCKED` | 423 |
| Business-rule violations — the long allow-list, plus any code starting with `ACCOUNT_` | 422 |
| Order/claim/route/hub lifecycle violations (`ORDER_NOT_DRAFT`, `ORDER_INVALID_TRANSITION`, `CLAIM_INVALID_TRANSITION`, `ROUTE_INVALID_TRANSITION`, `HUB_HAS_PENDING_DELIVERIES`, …) | 409 |
| `ROLE_NOT_CONFIGURED` | 500 |
| **Anything not listed** | **500** |

The full allow-list is in `src/FreshFlow.API/Extensions/ErrorExtensions.cs` — it is the only
place to add a new code, and adding one there is mandatory, otherwise the endpoint answers 500.

Common Auth/Admin codes and their meanings are listed in [`enums.md`](./enums.md).

---

## 5. RBAC Summary

What each role can reach. `admin` can reach everything except the surfaces scoped to a single
operational identity (`driver`, `market_agent`, and restaurant self-service `me/*` routes).

| Role | Reachable areas |
|---|---|
| `admin` | Everything under `/admin`, full catalog + market CRUD, hubs, logistics, orders ops, claims decisions, credit control, analytics, invoices, market sessions (read + write) |
| `operations_manager` | Analytics, market sessions (read), order groups (read), order ops (`actual-quantity`, `advance-status`), claims decisions, hub operations + discrepancy acknowledge, logistics routes/vehicles, invoices, market assignments |
| `market_agent` | `GET /pricing/assigned-markets`, market-product `price` / `quantity` / `tags` updates, `GET/PATCH /procurement/tasks/*`, `GET /products` |
| `hub_staff` | All hub operations for assigned hubs, `GET /hubs/assigned`, read-only vehicles + routes, `POST /logistics/routes/{id}/assign-vehicle`, loading manifest, `GET /products`, `GET /analytics/hub-throughput` |
| `driver` | All of `/driver/*`, hub handover checkout, route loading manifest |
| `restaurant` | Own profile + tax profile + delivery addresses, favorites, orders and scheduled orders, order claims, own credit + statements, own invoices, `GET /market-sessions/availability`, `GET /analytics/price-trends`, notifications, AI assistant |

Beyond the role check, several endpoints enforce **resource-level ownership** in the handler —
a restaurant may only read its own credit, invoices, orders and assistant sessions; a market
agent may only touch markets it is assigned to (`MARKET_ACCESS_DENIED`); hub staff are scoped
to their assigned hubs (`HUB_ACCESS_DENIED`). Vehicle dispatch is deliberately fleet-wide for
`hub_staff`: there is one hub and a shared fleet.

---

## 6. Rate Limiting

Fixed-window limiters registered in `Program.cs`; every limit and window is overridable through
configuration (`RateLimiting:*`), which is how the integration tests avoid tripping them.

| Policy | Applied to | Partition | Default |
|---|---|---|---|
| `auth` | `AuthController` (all actions) | client IP | 10 requests / 1 min |
| `orders` | `OrdersController` (all actions) | user id, falling back to IP | 30 requests / 1 min |
| `assistant` | `AssistantController` | user id, falling back to IP | 15 requests / 1 min |

Exceeding a limit returns **429**. No other controller is rate-limited.

---

## 7. Media Uploads

Images are never uploaded through this API. The client asks for a **signed Cloudinary upload**,
uploads directly to Cloudinary, then submits the resulting URL with the normal create/update
call. Signing is shared (`FreshFlow.Infrastructure.Media`, `ICloudinarySignatureService`) but
each surface exposes its own endpoint so RBAC and the target folder stay explicit:

| Endpoint | Purpose |
|---|---|
| `POST /profile/me/avatar/upload-signature` | User avatar |
| `POST /restaurants/me/business-license/upload-signature` | Restaurant business licence |
| `POST /products/image/upload-signature` | Product image |
| `POST /categories/image/upload-signature` | Category image |
| `POST /markets/image/upload-signature` | Market image |
| `POST /orders/{orderId}/claims/upload-signature` | Claim evidence photo |
| `POST /procurement/tasks/{batchId}/exceptions/upload-signature` | Procurement exception photo |
| `POST /hubs/{hubId}/inbound/{inboundId}/discrepancy/upload-signature` | Hub discrepancy photo |
| `POST /driver/deliveries/{deliveryId}/proof-of-delivery/upload-signature` | Proof of delivery |

---

## Changelog

| Version | Date | Change |
|---|---|---|
| 2.0 | 2026-08-22 | Regenerated from the controllers. Full 223-endpoint inventory; removed the v1.0 design draft and its `[PLANNED]` sections (Logistics, Hub, Analytics and `order-groups` are all implemented); added `NotificationHub`; per-field request/response examples dropped in favour of Swagger/Scalar. |
| 1.1 | 2026-06-30 | Part A implemented surface added alongside the v1.0 design. |
| 1.0 | 2026-05-09 | Original pre-implementation design. |
