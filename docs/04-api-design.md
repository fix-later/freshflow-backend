# FreshFlow (FFX) — API Design Document

**Version:** 1.1  
**Date:** 2026-05-09 (design) · **Reconciled with code:** 2026-06-30  
**Project:** FreshFlow – Intermediary Platform for Food Procurement and Logistics Optimization  
**Status:** Partially implemented — see Sync Status below  
**Based on:** Requirements Specification v1.0 + System Architecture v1.0 + Database Schema v1.0

---

## ⚙️ Sync Status (reviewed 2026-06-30)

> **Physical source of truth:** the ASP.NET Core controllers under
> `src/FreshFlow.API/Controllers/` and the SignalR hub mappings in
> `src/FreshFlow.API/Program.cs`. This document is split into two parts, mirroring
> `docs/03-database-schema.dbml`:
>
> - **[Part A — Implemented API Surface](#2-implemented-api-surface-part-a--authoritative)**
>   is the authoritative inventory of every route that physically exists today
>   (method, path, auth/roles). It matches the controllers 1:1.
> - **Part B** is the remaining original v1.0 design detail (Sections 3–6 below).
>   Sections/endpoints that are **not yet implemented** are marked `[PLANNED]`;
>   request/response examples for implemented endpoints are illustrative — the
>   binding contract is the code (and the generated Swagger/OpenAPI document).

**Implemented modules:** Auth, Profile, Admin, Restaurant Profile, Restaurant Credit (B2B công nợ),
Catalog (Categories, Units, Products, Markets/Market-Products), Pricing, Orders (+ Scheduled Orders),
AI Assistant, and the `PricingHub` / `OrderHub` real-time hubs.

**Not yet implemented (`[PLANNED]`):** Logistics (routes/vehicles), Hub operations,
Analytics, `order-groups` + auto-batch, `system-config`, `DeliveryHub`, and the generic
`PATCH /orders/{orderId}/status` (superseded by explicit lifecycle actions).

**Key deltas vs the v1.0 design:**
- The Payment/Billing gateway endpoints are superseded by a **B2B credit / công nợ** model
  (`/restaurants/{id}/credit`, `/admin/.../credit/settle|limit`).
- Catalog was normalized — new `/categories` and `/units` endpoints; `/products` and
  `/markets` gained full CRUD beyond the original read-only design.
- The order lifecycle is modeled with explicit actions (draft → items → confirm → receipt →
  issues / reorder) instead of a single `PATCH .../status`.
- `PATCH /admin/users/{id}/status` was implemented as `PATCH /admin/users/{id}/activate`.

---

## Table of Contents

1. [API Conventions](#1-api-conventions)
2. [Implemented API Surface (Part A — authoritative)](#2-implemented-api-surface-part-a--authoritative)
3. [Authentication Endpoints](#3-authentication-endpoints)
4. [Domain Endpoints](#4-domain-endpoints)
   - 4.1 [Pricing Domain](#41-pricing-domain)
   - 4.2 [Orders Domain](#42-orders-domain)
   - 4.3 [Logistics Domain `[PLANNED]`](#43-logistics-domain-planned)
   - 4.4 [Hub Domain `[PLANNED]`](#44-hub-domain-planned)
   - 4.5 [Analytics Domain `[PLANNED]`](#45-analytics-domain-planned)
   - 4.6 [Admin Domain](#46-admin-domain)
5. [SignalR Hubs](#5-signalr-hubs)
6. [Validation Rules](#6-validation-rules)
7. [API Security](#7-api-security)

---

## 1. API Conventions

### 1.1 Base URL

```
https://api.freshflow.vn/api/v1
```

### 1.2 Versioning

URL path versioning is used. The current version is `/api/v1/`. Breaking changes that cannot be backward-compatible are released under `/api/v2/`. Non-breaking additions (new optional fields, new endpoints) are made to the existing version without a version bump.

### 1.3 Response Envelope

All responses are wrapped in a consistent JSON envelope.

**Success response (non-paginated):**

```json
{
  "success": true,
  "data": { }
}
```

**Success response (paginated — offset-based):**

```json
{
  "success": true,
  "data": [ ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 150
  }
}
```

**Success response (paginated — cursor-based):**

```json
{
  "success": true,
  "data": [ ],
  "meta": {
    "pageSize": 20,
    "nextCursor": "eyJpZCI6InV1aWQiLCJjcmVhdGVkQXQiOiIyMDI2LTA1LTA5VDAzOjAwOjAwWiJ9"
  }
}
```

The `meta` field is omitted for non-paginated responses. `nextCursor` is `null` when there are no further pages. The cursor is a base64-encoded JSON object containing `{ "id": "uuid", "createdAt": "ISO8601" }`.

**Error response:**

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields failed validation.",
    "details": [
      { "field": "price", "message": "Must be greater than 0" }
    ]
  }
}
```

The `details` array is included only when `code` is `VALIDATION_ERROR` and field-level detail is available. It is omitted for other error types.

### 1.4 HTTP Status Codes

| Code | Meaning | When Used |
|------|---------|-----------|
| 200 OK | Success | Successful GET or PATCH |
| 201 Created | Resource created | Successful POST that creates a resource |
| 204 No Content | Success (no body) | Successful DELETE; logout |
| 400 Bad Request | Malformed request | Unparseable JSON body; missing required field at syntax level |
| 401 Unauthorized | Not authenticated | Missing, malformed, or expired JWT |
| 403 Forbidden | Not authorized | Valid JWT but insufficient role or resource ownership |
| 404 Not Found | Resource not found | Resource does not exist or is soft-deleted |
| 409 Conflict | Conflict | Optimistic concurrency conflict; duplicate resource; token reuse |
| 422 Unprocessable Entity | Business rule violation | Validation logic passed but business rule prevents action (e.g., order for out-of-stock item, cancelling a delivered order) |
| 429 Too Many Requests | Rate limit exceeded | Rate limit hit; `Retry-After` header included in response |
| 500 Internal Server Error | Server error | Unhandled exception |

### 1.5 Pagination

**Cursor-based pagination** is used for high-volume, append-heavy lists:
- Price history (`price_snapshots`)
- Notifications

**Offset-based pagination** is used for admin lists and lower-volume queries:
- Admin user lists, order lists, analytics exports

To request the next page in cursor pagination, pass the `nextCursor` value from the previous response as a query parameter `?cursor=<value>`. Do not decode or construct cursor values manually — treat them as opaque strings.

To request a specific page in offset-based pagination, use `?page=<n>&pageSize=<n>`.

### 1.6 Date and Time

All date-time values use **ISO 8601** format with timezone offset. Values are stored as UTC internally and returned with the `+07:00` offset for Ho Chi Minh City local context.

Example: `2026-05-09T03:00:00+07:00`

When accepting date-time inputs, any valid ISO 8601 value with timezone information is accepted. The server normalizes to UTC for storage.

### 1.7 Field Naming

Request bodies and response bodies use **camelCase** field names. Database column names (snake_case) are never exposed directly in the API surface.

### 1.8 Soft-Deleted Resources

Soft-deleted resources (where `deleted_at IS NOT NULL`) are treated as non-existent. Any request targeting a soft-deleted resource returns **404 Not Found**, not the deleted record. Admin endpoints may include an optional `?includeDeleted=true` query parameter where documented.

### 1.9 Authentication

All endpoints require a valid JWT Bearer token in the `Authorization` header **except** the public auth endpoints: `/api/v1/auth/login`, `/api/v1/auth/refresh`, `/api/v1/auth/forgot-password`, `/api/v1/auth/reset-password`, `/api/v1/auth/verify/request`, `/api/v1/auth/verify`, and `/api/v1/auth/register`.

```
Authorization: Bearer <accessToken>
```

For SignalR connections, the token is passed as a query string parameter during the negotiate handshake: `?access_token=<accessToken>`.

JWT access tokens are stateless and live for 15 minutes. Refresh tokens live for 7 days and are stored only as hashes in `refresh_tokens`.

Role values exposed by the API are lowercase `roles.name` values. Seeded values are: `admin`, `operations_manager`, `market_agent`, `hub_staff`, `driver`, and `restaurant`. The legacy request value `kiosk_staff` is accepted only as an alias for `market_agent` during Admin user creation. Each user has exactly one global role through `users.role_id`.

**Session expiry (FR-AUTH-008):** a request carrying an expired access token returns HTTP 401 with error code `TOKEN_EXPIRED`; a malformed or tampered token returns HTTP 401 with `UNAUTHORIZED`. While the refresh token is valid, the client obtains a new access token via `POST /api/v1/auth/refresh` (FR-AUTH-007). Once the refresh token is expired, revoked, or its token family is invalidated, the client must log in again.

**Phone OTP scope:** v1 does not implement SMS delivery or phone OTP verification. Phone numbers may still be stored on user profiles and used as a login identifier when present. Password reset and verification flows use email only; `PHONE` is reserved for a future version and returns `CHANNEL_NOT_SUPPORTED` where a channel field is accepted.

---

## 2. Implemented API Surface (Part A — authoritative)

This is the complete list of routes that exist in code today, grouped by controller.
All paths are prefixed with the base URL (`/api/v1`). "Auth" = any authenticated user;
otherwise the listed roles are required (`[Authorize(Roles = …)]`). Detailed
request/response shapes for these routes live in Sections 3–4 (where present) and in the
generated Swagger document.

### A1. Authentication — `AuthController` (`/auth`)

| Method | Path | Auth |
|--------|------|------|
| POST | `/auth/register` | Anonymous |
| POST | `/auth/login` | Anonymous |
| POST | `/auth/refresh` | Anonymous |
| POST | `/auth/logout` | Auth |
| POST | `/auth/forgot-password` | Anonymous |
| POST | `/auth/reset-password` | Anonymous |
| POST | `/auth/verify/request` | Anonymous |
| POST | `/auth/verify` | Anonymous |
| POST | `/auth/change-password` | Auth |

### A2. Profile — `ProfileController` (`/profile`)

| Method | Path | Auth |
|--------|------|------|
| GET | `/profile/me` | Auth |
| PUT | `/profile/me` | Auth |

### A3. Admin — `AdminController` (`/admin`)

| Method | Path | Roles |
|--------|------|-------|
| POST | `/admin/users` | admin |
| GET | `/admin/users` | admin |
| PATCH | `/admin/users/{userId}/activate` | admin |
| POST | `/admin/users/{userId}/unlock` | admin |
| GET | `/admin/roles` | admin |
| PATCH | `/admin/users/{userId}/role` | admin |
| PATCH | `/admin/restaurants/{restaurantId}/approve` | admin |
| POST | `/admin/restaurants/{restaurantId}/credit/settle` | admin |
| PUT | `/admin/restaurants/{restaurantId}/credit/limit` | admin |
| GET | `/admin/users/{userId}/market-assignments` | admin, operations_manager |
| PUT | `/admin/users/{userId}/market-assignments` | admin, operations_manager |

### A4. Restaurant Profile — `RestaurantProfileController` (`/restaurants`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/restaurants/me/approval-status` | restaurant |
| GET | `/restaurants/me/profile` | restaurant |
| PUT | `/restaurants/me/profile` | restaurant |
| GET | `/restaurants/me/delivery-addresses` | restaurant |
| POST | `/restaurants/me/delivery-addresses` | restaurant |
| PUT | `/restaurants/me/delivery-addresses/{id}` | restaurant |
| DELETE | `/restaurants/me/delivery-addresses/{id}` | restaurant |

### A5. Restaurant Credit (B2B công nợ) — `RestaurantCreditController` (`/restaurants/{restaurantId}/credit`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/restaurants/{restaurantId}/credit` | admin, restaurant |
| GET | `/restaurants/{restaurantId}/credit/transactions` | admin, restaurant |

### A6. Catalog — Categories — `CategoriesController` (`/categories`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/categories` | Auth |
| GET | `/categories/{id}` | Auth |
| POST | `/categories` | admin |
| PUT | `/categories/{id}` | admin |
| PATCH | `/categories/{id}/deactivate` | admin |

### A7. Catalog — Units — `UnitsController` (`/units`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/units` | Auth |
| GET | `/units/{id}` | Auth |
| POST | `/units` | admin |
| PUT | `/units/{id}` | admin |
| PATCH | `/units/{id}/deactivate` | admin |

### A8. Catalog — Products — `ProductsController` (`/products`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/products` | admin, operations_manager, market_agent, hub_staff, restaurant |
| GET | `/products/{id}` | admin, operations_manager, market_agent, hub_staff, restaurant |
| POST | `/products` | admin |
| PUT | `/products/{id}` | admin |
| PATCH | `/products/{id}/deactivate` | admin |

### A9. Markets & Market Products — `MarketsController` (`/markets`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/markets` | Auth |
| GET | `/markets/{id}` | Auth |
| POST | `/markets` | admin |
| PUT | `/markets/{id}` | admin |
| PATCH | `/markets/{id}/deactivate` | admin |
| DELETE | `/markets/{id}` | admin |
| GET | `/markets/{marketId}/products` | Auth |
| POST | `/markets/{marketId}/products` | admin |
| GET | `/markets/{marketId}/products/{productId}/price-history` | Auth |
| PATCH | `/markets/{marketId}/products/{productId}/price` | market_agent |
| PATCH | `/markets/{marketId}/products/{productId}/quantity` | market_agent |

### A10. Pricing — `PricingController` (`/pricing`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/pricing/assigned-markets` | market_agent |

### A11. Orders — `OrdersController` (`/orders`)

Base roles for the controller: `admin, operations_manager, restaurant` (overridden per action).

| Method | Path | Roles |
|--------|------|-------|
| GET | `/orders` | admin, operations_manager, restaurant |
| GET | `/orders/history` | admin, operations_manager, restaurant |
| GET | `/orders/{orderId}` | admin, operations_manager, restaurant |
| POST | `/orders` | restaurant |
| POST | `/orders/{orderId}/items` | restaurant |
| PUT | `/orders/{orderId}/items/{itemId}` | restaurant |
| DELETE | `/orders/{orderId}/items/{itemId}` | restaurant |
| GET | `/orders/{orderId}/confirm-preview` | restaurant |
| POST | `/orders/{orderId}/confirm` | restaurant |
| PATCH | `/orders/{orderId}/cancel` | admin, restaurant |
| PATCH | `/orders/{orderId}/items/{itemId}/actual-quantity` | admin, operations_manager |
| PATCH | `/orders/{orderId}/receipt` | restaurant |
| POST | `/orders/{orderId}/issues` | restaurant |
| POST | `/orders/{orderId}/reorder` | restaurant |

### A12. Scheduled Orders — `OrdersController` (`/orders/scheduled`)

| Method | Path | Roles |
|--------|------|-------|
| GET | `/orders/scheduled` | admin, restaurant |
| GET | `/orders/scheduled/{scheduledOrderId}` | admin, restaurant |
| GET | `/orders/scheduled/{scheduledOrderId}/instances` | admin, restaurant |
| POST | `/orders/scheduled` | restaurant |
| PATCH | `/orders/scheduled/{scheduledOrderId}` | admin, restaurant |
| PATCH | `/orders/scheduled/{scheduledOrderId}/cancel` | admin, restaurant |

### A13. AI Shopping Assistant — `AssistantController` (`/assistant`)

| Method | Path | Roles |
|--------|------|-------|
| POST | `/assistant/chat` | restaurant |

### A14. Real-time Hubs — `Program.cs`

| Hub | Path | Notes |
|-----|------|-------|
| `PricingHub` | `/hubs/pricing` | JWT via `?access_token=` on negotiate |
| `OrderHub` | `/hubs/orders` | JWT via `?access_token=` on negotiate |

---

## 3. Authentication Endpoints

#### Endpoint Summary

| UC | Method | Path | Role | Description |
|----|--------|------|------|-------------|
| UC-AUTH-01 | POST | `/api/v1/auth/login` | Public | Login with email/phone and password |
| UC-AUTH-02 | POST | `/api/v1/auth/logout` | Authenticated | Revoke the current refresh token |
| UC-AUTH-03 | POST | `/api/v1/auth/forgot-password` | Public | Request email password reset link |
| UC-AUTH-04 | POST | `/api/v1/auth/reset-password` | Public | Set a new password using email reset token |
| UC-AUTH-05 | POST | `/api/v1/auth/change-password` | Authenticated | Change own password |
| UC-AUTH-06 | POST | `/api/v1/auth/verify/request` | Public | Request email verification code |
| UC-AUTH-06 | POST | `/api/v1/auth/verify` | Public | Verify email code |
| UC-AUTH-07 | POST | `/api/v1/auth/refresh` | Public | Rotate refresh token and issue new token pair |
| UC-AUTH-08 | All protected endpoints | N/A | Middleware | Reject expired/invalid access token |
| UC-AUTH-09 | POST | `/api/v1/admin/users/{userId}/unlock` | Admin | Clear temporary account lock |
| UC-AUTH-10 | GET/PATCH | `/api/v1/admin/roles`, `/api/v1/admin/users/{userId}/role` | Admin | Manage global role assignment |
| UC-AUTH-11 | POST | `/api/v1/auth/register` | Public | Restaurant self-registration (pending approval) |

### POST /api/v1/auth/login

**Role:** Public (no authentication required)

Authenticates a user with email or phone number and password. Returns a signed JWT access token and an opaque refresh token on success.

**Request body:**

```json
{
  "identifier": "manager@phobaatu.vn",
  "password": "MySecureP@ss1"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `identifier` | string | Yes | Email address or phone number, max 255 characters |
| `password` | string | Yes | Non-empty string |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "accessToken": "jwt-access-token",
    "refreshToken": "a3f8d2c1e7b94f6a82d0c5e1f3b7a9d2",
    "expiresIn": 900,
    "user": {
      "id": "7f3a1c42-d491-4568-b79a-f1e3c8ef9a2b",
      "email": "manager@phobaatu.vn",
      "phone": "+84901234567",
      "fullName": "Nguyen Van A",
      "role": "restaurant",
      "status": "ACTIVE",
      "emailVerified": true,
      "phoneVerified": false
    }
  }
}
```

| Field | Description |
|-------|-------------|
| `accessToken` | Signed JWT. TTL 15 minutes. Contains `sub`, `email`, `role`, `iat`, and `exp` claims. |
| `refreshToken` | Opaque random string. TTL 7 days. Stored as `refresh_tokens.token_hash`; raw token is never persisted. |
| `expiresIn` | Access token TTL in seconds. Always `900`. |
| `user.role` | The user's single global role from `roles.name`. |

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `identifier` or `password` field is missing or malformed |
| 401 Unauthorized | `INVALID_CREDENTIALS` | Identifier not found or password is incorrect. Same error code for both cases to prevent user enumeration. |
| 422 Unprocessable Entity | `ACCOUNT_INACTIVE` | `users.status` is `SUSPENDED` or `DELETED`, or `deleted_at IS NOT NULL` |
| 423 Locked | `ACCOUNT_LOCKED` | `users.locked_until` is in the future |

**Account lockout (FR-AUTH-009):** after 5 consecutive failed login attempts, `users.failed_login_count` reaches the threshold and `users.locked_until` is set to now + 15 minutes. A successful login resets `failed_login_count` to 0 and clears any expired lock. Admin can unlock manually through `POST /api/v1/admin/users/{userId}/unlock`.

---

### POST /api/v1/auth/refresh

**Role:** Public (no authentication required — refresh token is the credential)

Exchanges a valid refresh token for a new access token and a new refresh token. Implements refresh token rotation: the submitted token is immediately invalidated and a new token is issued. Token reuse (submitting an already-used token) triggers invalidation of the entire token family.

**Request body:**

```json
{
  "refreshToken": "a3f8d2c1e7b94f6a82d0c5e1f3b7a9d2"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `refreshToken` | string | Yes | Non-empty string |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "accessToken": "new-jwt-access-token",
    "refreshToken": "b9c7e4f2a1d38e5c91b6a4e2f8c0d7e3",
    "expiresIn": 900
  }
}
```

The submitted token row is marked with `revoked_at`, and the new token row is created with the same `family_id`; the old row points to the new row through `replaced_by_token_id`. If a revoked token is submitted again, the entire `family_id` is revoked.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `refreshToken` field is missing |
| 401 Unauthorized | `REFRESH_TOKEN_INVALID` | Token hash is not found |
| 401 Unauthorized | `REFRESH_TOKEN_EXPIRED` | Token is past its 7-day TTL |
| 409 Conflict | `REFRESH_TOKEN_REUSE` | Token was already revoked or replaced. The entire token family is invalidated. |

---

### POST /api/v1/auth/logout

**Role:** Any authenticated user

Revokes a specific refresh token. The access token associated with this session remains valid until its own TTL expires (maximum 15 minutes). Other concurrent sessions (other devices) are not affected.

**Request header:** `Authorization: Bearer <accessToken>`

**Request body:**

```json
{
  "refreshToken": "a3f8d2c1e7b94f6a82d0c5e1f3b7a9d2"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `refreshToken` | string | Yes | Non-empty string |

**Success response — 204 No Content**

No response body.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid access token in the Authorization header |

Note: If the `refreshToken` in the body is already revoked or does not match the authenticated user, the server still returns 204. This prevents oracle attacks on token existence.

---

### POST /api/v1/auth/forgot-password

**Role:** Public (no authentication required)

Initiates a password reset (FR-AUTH-003). The user submits their registered email address. If the email belongs to an active account, the system creates a single-use credential in `password_reset_tokens` and dispatches a reset link by email. SMS and phone OTP password reset are deferred in v1.

**Request body:**

```json
{
  "identifier": "manager@phobaatu.vn"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `identifier` | string | Yes | Registered email address |

**Success response — 202 Accepted**

```json
{
  "success": true,
  "data": null
}
```

The response is **always** 202, whether or not the identifier matches an account, to prevent user enumeration. If it matches, an email reset link is dispatched. The credential is single-use and expires after 15 minutes; issuing a new one invalidates any previously issued credential for that account.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `identifier` field is missing or is not a valid email address |
| 429 Too Many Requests | `RATE_LIMITED` | Too many reset requests for the same identifier/IP in a short window |

---

### POST /api/v1/auth/reset-password

**Role:** Public (no authentication required — the reset credential is the proof of identity)

Sets a new password using a valid email reset token from `forgot-password` (FR-AUTH-004).

**Request body:**

```json
{
  "token": "f1e3c8ef9a2b7f3a1c42d4914568b79a",
  "newPassword": "MyNewSecureP@ss1"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `token` | string | Yes | The reset link token issued by `forgot-password` |
| `newPassword` | string | Yes | Must satisfy the password strength policy |

**Success response — 200 OK**

```json
{
  "success": true,
  "data": null
}
```

On success, `users.password_hash` is updated, the reset credential is marked `used_at`, and all existing refresh-token families for the user are revoked to force re-login on every device.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `RESET_TOKEN_INVALID` | Token does not match any pending reset |
| 400 Bad Request | `RESET_TOKEN_EXPIRED` | Token has expired or was already used |
| 400 Bad Request | `WEAK_PASSWORD` | `newPassword` fails the strength policy |

---

### POST /api/v1/auth/change-password

**Role:** Any authenticated user

Changes the authenticated user's own password (FR-AUTH-005). Requires the current password as a re-authentication step.

**Request header:** `Authorization: Bearer <accessToken>`

**Request body:**

```json
{
  "currentPassword": "MySecureP@ss1",
  "newPassword": "MyNewSecureP@ss1"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `currentPassword` | string | Yes | Non-empty string |
| `newPassword` | string | Yes | Must satisfy the password strength policy; must differ from `currentPassword` |

**Success response — 204 No Content**

On success, all of the user's active refresh tokens are revoked. Existing stateless access tokens remain valid until their normal `exp`, but any subsequent refresh requires the user to log in again.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Missing required field, weak `newPassword`, or `newPassword` equals `currentPassword` |
| 401 Unauthorized | `INVALID_CURRENT_PASSWORD` | `currentPassword` is incorrect |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid access token |

No email, SMS, or phone OTP is sent by this flow in v1.

---

### POST /api/v1/auth/verify/request

**Role:** Public (no authentication required)

Sends a verification code to a user's email (FR-AUTH-006). If the identifier belongs to a user, the system stores a hashed code in `verification_codes` with a short TTL. The response does not reveal whether the identifier exists. Phone verification by SMS/OTP is deferred in v1.

**Request body:**

```json
{
  "identifier": "manager@phobaatu.vn",
  "channel": "EMAIL"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `identifier` | string | Yes | Email address to verify |
| `channel` | string | Yes | Must be `EMAIL` in v1. `PHONE` is reserved for future SMS/OTP support. |

**Success response — 202 Accepted** — a verification code is dispatched. Returns 202 regardless of account existence (no enumeration).

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `identifier` or `channel` is missing/invalid |
| 422 Unprocessable Entity | `CHANNEL_NOT_SUPPORTED` | `channel = PHONE` because SMS/phone OTP is not implemented in v1 |
| 429 Too Many Requests | `RATE_LIMITED` | Too many code requests in a short window |

---

### POST /api/v1/auth/verify

**Role:** Public (no authentication required — the code is the proof)

Confirms ownership of an email by submitting the verification code (FR-AUTH-006). Phone verification by SMS/OTP is deferred in v1.

**Request body:**

```json
{
  "identifier": "manager@phobaatu.vn",
  "channel": "EMAIL",
  "code": "482915"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `identifier` | string | Yes | Email address being verified |
| `channel` | string | Yes | Must be `EMAIL` in v1. `PHONE` is reserved for future SMS/OTP support. |
| `code` | string | Yes | The verification code that was sent |

**Success response — 200 OK** — the email channel is marked verified (`email_verified_at` set). Re-verifying an already-verified email is idempotent and also returns 200.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `OTP_INVALID` | Code is incorrect, expired, or already used |
| 422 Unprocessable Entity | `CHANNEL_NOT_SUPPORTED` | `channel = PHONE` because SMS/phone OTP is not implemented in v1 |

---

### POST /api/v1/auth/register

**Role:** Public (no authentication required)

Allows a restaurant owner to self-register an account (UC-AUTH-11). The system creates a `User` with role `restaurant` and a linked `Restaurant` profile with `is_approved = false`. The account is active immediately but cannot place orders until an Admin approves it via `PATCH /api/v1/admin/restaurants/{restaurantId}/approve`.

**Request body:**

```json
{
  "email": "owner@phobaatu.vn",
  "password": "MySecureP@ss1",
  "restaurantName": "Phở Bà Tú",
  "phone": "+84901234567"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `email` | string | Yes | Valid email format; must not already exist |
| `password` | string | Yes | Must satisfy the password strength policy (min 8 chars, uppercase, digit, special char) |
| `restaurantName` | string | Yes | Non-empty, max 255 characters |
| `phone` | string | No | Valid phone format if provided |

**Success response — 201 Created**

```json
{
  "success": true,
  "data": {
    "userId": "uuid",
    "restaurantId": "uuid",
    "email": "owner@phobaatu.vn",
    "restaurantName": "Phở Bà Tú",
    "isApproved": false
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 409 Conflict | `EMAIL_ALREADY_EXISTS` | Email is already registered |
| 400 Bad Request | `VALIDATION_ERROR` | Missing required field or invalid format |
| 400 Bad Request | `WEAK_PASSWORD` | `password` fails the strength policy |

---

Admin-managed user creation, role assignment, account status, and account unlock are documented under the Admin Domain.

---

## 4. Domain Endpoints

> **Reconciliation note:** the sections below are the original v1.0 design detail (Part B).
> For the authoritative list of what is actually deployed, see [Part A](#2-implemented-api-surface-part-a--authoritative).
> **Pricing (4.1), Orders (4.2), and Admin (4.6)** are implemented but have evolved — the
> live routes, paths, and roles are those in Part A; individual endpoints here that were
> dropped or changed are tagged `[PLANNED]`, `[SUPERSEDED]`, or `[CHANGED]`.
> **Logistics (4.3), Hub (4.4), and Analytics (4.5)** are entirely `[PLANNED]` (no controllers
> exist yet). Catalog endpoints (`/categories`, `/units`, `/products`, `/markets`),
> Profile, Restaurant Profile, Restaurant Credit, and the AI Assistant are implemented but
> were added after v1.0 — they are catalogued in Part A (detailed bodies live in Swagger).

### 4.1 Pricing Domain

#### Endpoint Summary

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/api/v1/markets` | Any authenticated | List all active markets |
| GET | `/api/v1/markets/{marketId}/products` | Any authenticated | List products at a market with current price/quantity |
| GET | `/api/v1/markets/{marketId}/products/{productId}/price-history` | Any authenticated | Get price history for a market product |
| PATCH | `/api/v1/markets/{marketId}/products/{productId}/price` | Market Agent | Update price and/or quantity for a market product |
| GET | `/api/v1/products` | Admin, Market Agent | List all products in the system catalog |
| POST | `/api/v1/products` | Admin | Create a new product in the catalog |

---

#### GET /api/v1/markets

**Role:** Any authenticated user

Returns all active markets. Soft-deleted or inactive markets are excluded.

**Query parameters:** None

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
      "name": "Chợ đầu mối Hóc Môn",
      "address": "Xã Xuân Thới Sơn, Huyện Hóc Môn, TP.HCM",
      "latitude": 10.8912,
      "longitude": 106.5981,
      "isActive": true,
      "createdAt": "2026-01-01T07:00:00+07:00"
    },
    {
      "id": "b2c3d4e5-f6a7-8901-bcde-f01234567890",
      "name": "Chợ đầu mối Bình Điền",
      "address": "Đường Nguyễn Văn Linh, Quận 8, TP.HCM",
      "latitude": 10.7230,
      "longitude": 106.6050,
      "isActive": true,
      "createdAt": "2026-01-01T07:00:00+07:00"
    },
    {
      "id": "c3d4e5f6-a7b8-9012-cdef-012345678901",
      "name": "Chợ đầu mối Thủ Đức",
      "address": "Phường Trường Thọ, TP. Thủ Đức, TP.HCM",
      "latitude": 10.8567,
      "longitude": 106.7547,
      "isActive": true,
      "createdAt": "2026-01-01T07:00:00+07:00"
    }
  ]
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |

---

#### GET /api/v1/markets/{marketId}/products

**Role:** Any authenticated user

Returns all active products at a specific market, including current price and available quantity. Cursor-paginated.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `marketId` | UUID | The market to query |

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | string | No | Filter by product category (exact match) |
| `cursor` | string | No | Pagination cursor from previous response |
| `pageSize` | integer | No | Number of results per page. Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
      "productId": "e5f6a7b8-c9d0-1234-efab-234567890123",
      "marketId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
      "productName": "Cá lóc",
      "category": "thủy hải sản",
      "unit": "kg",
      "currentPrice": 125000.00,
      "currentQuantity": 500,
      "availableQuantity": 480,
      "updatedAt": "2026-05-09T03:45:00+07:00",
      "updatedBy": "f6a7b8c9-d0e1-2345-fabc-345678901234"
    }
  ],
  "meta": {
    "pageSize": 20,
    "nextCursor": "eyJpZCI6ImQ0ZTVmNmE3LWI4YzktMDEyMy1kZWZhLTEyMzQ1Njc4OTAxMiIsImNyZWF0ZWRBdCI6IjIwMjYtMDUtMDlUMDM6NDU6MDBaIn0="
  }
}
```

`availableQuantity` = `currentQuantity` minus soft-reserved quantity (from Redis). `updatedBy` is the user ID of the last Market Agent who updated this record.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 404 Not Found | `MARKET_NOT_FOUND` | `marketId` does not match any active market |

---

#### GET /api/v1/markets/{marketId}/products/{productId}/price-history

**Role:** Any authenticated user

Returns the immutable price snapshot history for a specific product at a specific market, sorted descending by `recordedAt`. Cursor-paginated. Supports date range filtering.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `marketId` | UUID | The market |
| `productId` | UUID | The product in the catalog |

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `from` | ISO 8601 date | No | Start of date range (inclusive). Example: `2026-05-01` |
| `to` | ISO 8601 date | No | End of date range (inclusive). Example: `2026-05-09` |
| `cursor` | string | No | Pagination cursor |
| `pageSize` | integer | No | Default: 50. Max: 200. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "f7a8b9c0-d1e2-3456-abcd-456789012345",
      "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
      "price": 125000.00,
      "quantity": 500,
      "recordedBy": "f6a7b8c9-d0e1-2345-fabc-345678901234",
      "recordedAt": "2026-05-09T03:45:00+07:00"
    },
    {
      "id": "a8b9c0d1-e2f3-4567-bcde-567890123456",
      "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
      "price": 118000.00,
      "quantity": 600,
      "recordedBy": "f6a7b8c9-d0e1-2345-fabc-345678901234",
      "recordedAt": "2026-05-09T02:30:00+07:00"
    }
  ],
  "meta": {
    "pageSize": 50,
    "nextCursor": "eyJpZCI6ImE4YjljMGQxLWUyZjMtNDU2Ny1iY2RlLTU2Nzg5MDEyMzQ1NiIsImNyZWF0ZWRBdCI6IjIwMjYtMDUtMDlUMDI6MzA6MDBaIn0="
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `from` or `to` is not a valid date format |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 404 Not Found | `MARKET_NOT_FOUND` | `marketId` does not match an active market |
| 404 Not Found | `PRODUCT_NOT_FOUND` | `productId` does not match an active product, or the product is not listed at this market |

---

#### PATCH /api/v1/markets/{marketId}/products/{productId}/price

**Role:** Market Agent only

Updates the current price and/or available quantity of a product at a market. The Market Agent must be assigned to this specific market in `user_market_assignments`. On success, a new `price_snapshot` record is created, the `market_products` row is updated, the Redis cache is refreshed, and a `PriceUpdated` SignalR event is broadcast to all clients subscribed to `market:{marketId}`.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `marketId` | UUID | The market where the product is listed |
| `productId` | UUID | The product in the catalog |

**Request body:**

```json
{
  "price": 135000.00,
  "quantity": 420,
  "expectedVersion": "2026-05-09T03:45:00+07:00"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `price` | number | At least one of `price` or `quantity` | Must be > 0, max 2 decimal places |
| `quantity` | integer | At least one of `price` or `quantity` | Must be >= 0 |
| `expectedVersion` | ISO 8601 string | No | Optional optimistic concurrency check. If provided, must match the current `updatedAt` of the `market_products` row. If it does not match, HTTP 409 is returned. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
    "productId": "e5f6a7b8-c9d0-1234-efab-234567890123",
    "marketId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
    "previousPrice": 125000.00,
    "currentPrice": 135000.00,
    "currentQuantity": 420,
    "changePercent": 8.00,
    "updatedAt": "2026-05-09T04:10:00+07:00",
    "updatedBy": "f6a7b8c9-d0e1-2345-fabc-345678901234",
    "snapshotId": "b9c0d1e2-f3a4-5678-cdef-678901234567"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Request body is missing or malformed |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `MARKET_ACCESS_DENIED` | The authenticated Market Agent is not assigned to this `marketId` |
| 404 Not Found | `MARKET_NOT_FOUND` | `marketId` does not match an active market |
| 404 Not Found | `PRODUCT_NOT_FOUND` | `productId` is not listed at this market |
| 409 Conflict | `OPTIMISTIC_CONCURRENCY_CONFLICT` | `expectedVersion` was provided and does not match current `updatedAt`. Client must re-fetch and retry. |
| 422 Unprocessable Entity | `INVALID_PRICE` | `price` is 0 or negative |
| 422 Unprocessable Entity | `INVALID_QUANTITY` | `quantity` is negative or not an integer |

---

#### GET /api/v1/products

**Role:** Admin, Market Agent

Lists all products in the system-wide catalog. By default returns only active (non-soft-deleted) products.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `search` | string | No | Filter by product name (case-insensitive partial match) |
| `category` | string | No | Filter by category |
| `includeInactive` | boolean | No | Admin only. If `true`, includes soft-deleted products. Default: `false`. |
| `page` | integer | No | Default: 1 |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "e5f6a7b8-c9d0-1234-efab-234567890123",
      "name": "Cá lóc",
      "category": "thủy hải sản",
      "unit": "kg",
      "description": "Cá lóc đồng tươi sống",
      "isActive": true,
      "createdAt": "2026-01-01T07:00:00+07:00"
    }
  ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 10
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user does not have Admin or Market Agent access |

---

#### POST /api/v1/products

**Role:** Admin only

Creates a new product in the system-wide catalog. The product is then available to be associated with any market by an Admin.

**Request body:**

```json
{
  "name": "Tôm sú",
  "category": "thủy hải sản",
  "unit": "kg",
  "description": "Tôm sú size 20 con/kg"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `name` | string | Yes | Non-empty, max 200 characters |
| `category` | string | No | Max 100 characters |
| `unit` | string | Yes | Non-empty, max 50 characters (e.g., `"kg"`, `"bunch"`, `"piece"`) |
| `description` | string | No | Max 1000 characters |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "g7h8i9j0-k1l2-3456-mnop-789012345678",
    "name": "Tôm sú",
    "category": "thủy hải sản",
    "unit": "kg",
    "description": "Tôm sú size 20 con/kg",
    "isActive": true,
    "createdBy": "admin-user-uuid",
    "createdAt": "2026-05-09T10:00:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Required field missing or exceeds max length |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

### 4.2 Orders Domain

#### Endpoint Summary

| Method | Path | Role | Description |
|--------|------|------|-------------|
| POST | `/api/v1/orders` | Restaurant | Create a bulk order |
| GET | `/api/v1/orders` | Restaurant, Admin | List orders |
| GET | `/api/v1/orders/{orderId}` | Restaurant Manager/Staff (member restaurant), Admin | Get order detail |
| PATCH | `/api/v1/orders/{orderId}/cancel` | Restaurant Manager/Staff (member restaurant), Admin | Cancel an order |
| PATCH | `/api/v1/orders/{orderId}/status` | Admin | Update order status |
| GET | `/api/v1/order-groups` | Admin | List order groups |
| POST | `/api/v1/order-groups` | Admin | Create/lock an order group |
| POST | `/api/v1/admin/order-groups/auto-batch` | Admin | Trigger the same auto-batching service used by the 22:00 cutoff job |
| GET | `/api/v1/orders/scheduled` | Restaurant | List scheduled orders |

---

#### POST /api/v1/orders

**Role:** Restaurant Manager/Staff only

Creates a new bulk order. The restaurant must have `restaurants.status = ACTIVE`. Each line item references a `marketProductId` and specifies a quantity. Stock is validated against available quantity (current minus soft-reserved) in Redis before the order is created. On success, soft-reservations are applied in Redis.

**Request body:**

```json
{
  "items": [
    {
      "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
      "quantity": 50
    },
    {
      "marketProductId": "h8i9j0k1-l2m3-4567-nopq-890123456789",
      "quantity": 30
    }
  ],
  "scheduledFor": "2026-05-10T04:00:00+07:00",
  "notes": "Giao trước 5 giờ sáng"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `items` | array | Yes | Non-empty array, max 50 items |
| `items[].marketProductId` | UUID | Yes | Must reference an active market product |
| `items[].quantity` | integer | Yes | Must be > 0 |
| `scheduledFor` | ISO 8601 | No | If provided, must be at least 2 hours in the future from the time of the request |
| `notes` | string | No | Max 500 characters |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "i9j0k1l2-m3n4-5678-opqr-901234567890",
    "restaurantId": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
    "status": "pending",
    "items": [
      {
        "id": "k1l2m3n4-o5p6-7890-qrst-123456789012",
        "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
        "productName": "Cá lóc",
        "marketName": "Chợ đầu mối Hóc Môn",
        "quantity": 50,
        "unitPrice": 135000.00,
        "subtotal": 6750000.00
      }
    ],
    "totalAmount": 6750000.00,
    "scheduledFor": "2026-05-10T04:00:00+07:00",
    "notes": "Giao trước 5 giờ sáng",
    "createdAt": "2026-05-09T10:15:00+07:00"
  }
}
```

`unitPrice` is snapshotted from the current price at the time of order creation.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Request body is malformed or required fields are missing |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not a Restaurant |
| 422 Unprocessable Entity | `RESTAURANT_NOT_APPROVED` | The restaurant account has not been approved by Admin |
| 422 Unprocessable Entity | `EMPTY_ORDER` | `items` array is empty |
| 422 Unprocessable Entity | `INVALID_PRODUCT` | One or more `marketProductId` values do not reference active products. Response `details` identifies each offending item. |
| 422 Unprocessable Entity | `INSUFFICIENT_STOCK` | One or more items exceed available stock. Response includes per-item `requestedQty` and `availableQty` in `details`. |
| 422 Unprocessable Entity | `SCHEDULED_FOR_TOO_SOON` | `scheduledFor` is less than 2 hours from the request time |

**INSUFFICIENT_STOCK error detail example:**

```json
{
  "success": false,
  "error": {
    "code": "INSUFFICIENT_STOCK",
    "message": "Requested quantity exceeds available stock for one or more items.",
    "details": [
      {
        "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
        "productName": "Cá lóc",
        "requestedQty": 50,
        "availableQty": 30
      }
    ]
  }
}
```

---

#### GET /api/v1/orders

**Role:** Restaurant Manager/Staff (member restaurant orders only), Admin (all orders)

Returns a paginated list of orders. Restaurant users see only their own orders. Admin users see all orders across all restaurants.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `status` | string | No | Filter by order status. One of: `pending`, `confirmed`, `processing`, `ready_for_pickup`, `in_transit`, `delivered`, `cancelled` |
| `restaurantId` | UUID | No | Admin only. Filter by restaurant. |
| `from` | ISO 8601 date | No | Filter by `createdAt` start date |
| `to` | ISO 8601 date | No | Filter by `createdAt` end date |
| `sort` | string | No | Sort order. Default: `createdAt:desc`. Supports: `createdAt:asc`, `createdAt:desc` |
| `cursor` | string | No | Pagination cursor |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "i9j0k1l2-m3n4-5678-opqr-901234567890",
      "restaurantId": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
      "restaurantName": "Nhà hàng Phở Bà Tư",
      "status": "confirmed",
      "totalAmount": 6750000.00,
      "itemCount": 2,
      "scheduledFor": "2026-05-10T04:00:00+07:00",
      "createdAt": "2026-05-09T10:15:00+07:00"
    }
  ],
  "meta": {
    "pageSize": 20,
    "nextCursor": "eyJpZCI6Imk5ajBrMWwyLW0zbjQtNTY3OC1vcHFyLTkwMTIzNDU2Nzg5MCIsImNyZWF0ZWRBdCI6IjIwMjYtMDUtMDlUMTA6MTU6MDBaIn0="
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Restaurant user passes `restaurantId` for a different restaurant |

---

#### GET /api/v1/orders/{orderId}

**Role:** Restaurant Manager/Staff (member restaurant orders only), Admin (any order)

Returns the full detail of a single order, including all line items.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `orderId` | UUID | The order to retrieve |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "id": "i9j0k1l2-m3n4-5678-opqr-901234567890",
    "restaurantId": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
    "restaurantName": "Nhà hàng Phở Bà Tư",
    "status": "confirmed",
    "orderGroupId": null,
    "scheduledOrderId": null,
    "items": [
      {
        "id": "k1l2m3n4-o5p6-7890-qrst-123456789012",
        "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
        "productName": "Cá lóc",
        "marketName": "Chợ đầu mối Hóc Môn",
        "quantity": 50,
        "unitPrice": 135000.00,
        "subtotal": 6750000.00
      }
    ],
    "totalAmount": 6750000.00,
    "scheduledFor": "2026-05-10T04:00:00+07:00",
    "notes": "Giao trước 5 giờ sáng",
    "cancelledAt": null,
    "cancellationReason": null,
    "createdAt": "2026-05-09T10:15:00+07:00",
    "updatedAt": "2026-05-09T10:20:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Restaurant user requesting another restaurant's order |
| 404 Not Found | `ORDER_NOT_FOUND` | `orderId` does not exist or is soft-deleted |

---

#### PATCH /api/v1/orders/{orderId}/cancel

**Role:** Restaurant Manager/Staff (member restaurant order, cancellable status only), Admin (any order in any cancellable status)

Cancels an order. Transitions status to `cancelled` and releases soft-reservations for all line items in Redis.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `orderId` | UUID | The order to cancel |

**Request body:**

```json
{
  "reason": "Nhà hàng không cần hàng hôm nay"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `reason` | string | No | Max 500 characters |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "id": "i9j0k1l2-m3n4-5678-opqr-901234567890",
    "status": "cancelled",
    "cancelledAt": "2026-05-09T11:00:00+07:00",
    "cancellationReason": "Nhà hàng không cần hàng hôm nay"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Restaurant user attempting to cancel another restaurant's order |
| 404 Not Found | `ORDER_NOT_FOUND` | `orderId` does not exist |
| 409 Conflict | `ORDER_NOT_CANCELLABLE` | Order status is `in_transit`, `delivered`, or `processing`. Includes `currentStatus` in response `details`. |

---

#### PATCH /api/v1/orders/{orderId}/status `[SUPERSEDED]`

> `[SUPERSEDED]` Not implemented as a generic status patch. The lifecycle uses explicit
> actions instead: `POST .../confirm`, `PATCH .../cancel`, `PATCH .../receipt`, and
> `PATCH .../items/{itemId}/actual-quantity` (see Part A §A11).

**Role:** Admin only

Advances or updates the status of an order according to the order state machine. Valid transitions: `pending → confirmed`, `confirmed → processing`, `processing → ready_for_pickup`, `ready_for_pickup → in_transit`, `in_transit → delivered`. Admin may also cancel an order from any non-terminal state. On each status change, an `OrderStatusChanged` SignalR event is broadcast to the restaurant's group.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `orderId` | UUID | The order to update |

**Request body:**

```json
{
  "status": "confirmed"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `status` | string | Yes | Must be a valid `order_status` enum value: `confirmed`, `processing`, `ready_for_pickup`, `in_transit`, `delivered`, `cancelled` |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "id": "i9j0k1l2-m3n4-5678-opqr-901234567890",
    "previousStatus": "pending",
    "currentStatus": "confirmed",
    "updatedAt": "2026-05-09T11:30:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `status` is missing or not a valid enum value |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `ORDER_NOT_FOUND` | `orderId` does not exist |
| 422 Unprocessable Entity | `INVALID_STATUS_TRANSITION` | The requested status transition is not allowed from the current status. Response includes `currentStatus` and `allowedTransitions`. |

---

#### GET /api/v1/order-groups `[PLANNED]`

**Role:** Admin only

Returns a paginated list of all order groups with summary information.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `status` | string | No | Filter by group status: `open`, `locked`, `dispatched`, `completed` |
| `page` | integer | No | Default: 1 |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "l2m3n4o5-p6q7-8901-rstu-234567890123",
      "name": "Nhóm giao hàng sáng 10/5",
      "status": "locked",
      "totalOrders": 5,
      "groupedBy": "admin-user-uuid",
      "createdAt": "2026-05-09T22:00:00+07:00"
    }
  ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 3
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### POST /api/v1/order-groups `[PLANNED]`

**Role:** Admin only

Creates a new order group and associates the specified orders with it. All orders must have status `confirmed` and must not already belong to another active order group.

**Request body:**

```json
{
  "orderIds": [
    "i9j0k1l2-m3n4-5678-opqr-901234567890",
    "m3n4o5p6-q7r8-9012-stuv-345678901234",
    "n4o5p6q7-r8s9-0123-tuvw-456789012345"
  ],
  "name": "Nhóm giao hàng sáng 10/5"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `orderIds` | array of UUIDs | Yes | Non-empty, max 20 orders per group |
| `name` | string | No | Max 200 characters |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "l2m3n4o5-p6q7-8901-rstu-234567890123",
    "name": "Nhóm giao hàng sáng 10/5",
    "status": "locked",
    "totalOrders": 3,
    "orderIds": [
      "i9j0k1l2-m3n4-5678-opqr-901234567890",
      "m3n4o5p6-q7r8-9012-stuv-345678901234",
      "n4o5p6q7-r8s9-0123-tuvw-456789012345"
    ],
    "groupedBy": "admin-user-uuid",
    "createdAt": "2026-05-09T22:00:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `orderIds` is empty or contains invalid UUIDs |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 409 Conflict | `ORDER_ALREADY_GROUPED` | One or more orders are already in an active order group. Response `details` lists the conflicting order IDs. |
| 422 Unprocessable Entity | `ORDER_NOT_CONFIRMED` | One or more orders are not in `confirmed` status. Response `details` lists the offending order IDs and their current statuses. |

---

#### POST /api/v1/admin/order-groups/auto-batch `[PLANNED]`

**Role:** Admin only

Triggers the same batching service that normally runs at the 22:00 cutoff. The service finds eligible `CONFIRMED` orders that are not already assigned to an active batch, groups them by delivery zone and source market, creates `OrderGroup`/Procurement Batch records, transitions included orders to `BATCHED`, and emits `OrderGrouped` events. The operation is idempotent.

**Request body:**

```json
{
  "targetDate": "2026-05-31",
  "dryRun": false,
  "force": false
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `targetDate` | date (`YYYY-MM-DD`) | No | Defaults to current date in `Asia/Ho_Chi_Minh` |
| `dryRun` | boolean | No | Defaults to `false`; when `true`, returns a preview and does not write to PostgreSQL |
| `force` | boolean | No | Defaults to `false`; reserved for Admin recovery workflows and must not create duplicate active batches |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "targetDate": "2026-05-31",
    "dryRun": false,
    "createdBatchCount": 2,
    "batchedOrderCount": 12,
    "skippedOrderCount": 3,
    "batches": [
      {
        "orderGroupId": "l2m3n4o5-p6q7-8901-rstu-234567890123",
        "deliveryZone": "district-1",
        "sourceMarketId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
        "orderIds": [
          "i9j0k1l2-m3n4-5678-opqr-901234567890"
        ]
      }
    ],
    "skippedOrders": [
      {
        "orderId": "m3n4o5p6-q7r8-9012-stuv-345678901234",
        "reason": "ALREADY_BATCHED"
      }
    ],
    "triggeredBy": "admin-user-uuid",
    "triggeredAt": "2026-05-31T21:45:00+07:00"
  }
}
```

When `dryRun=true`, `createdBatchCount` and `batchedOrderCount` describe what would be created, while all `orderGroupId` values are `null`.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `targetDate` is not a valid date |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 409 Conflict | `AUTO_BATCH_ALREADY_RUNNING` | Another scheduled or manual auto-batch run is currently processing |

---

#### GET /api/v1/orders/scheduled

**Role:** Restaurant Manager/Staff only

Returns the list of active scheduled orders (recurring order templates) for the authenticated restaurant.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `page` | integer | No | Default: 1 |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "o5p6q7r8-s9t0-1234-uvwx-567890123456",
      "restaurantId": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
      "recurrenceType": "daily",
      "firstRunAt": "2026-05-10T04:00:00+07:00",
      "lastExecutedAt": null,
      "cancelledAt": null,
      "notes": "Đơn hàng hàng ngày",
      "createdAt": "2026-05-09T10:00:00+07:00"
    }
  ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 1
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not a Restaurant |

---

### 4.3 Logistics Domain `[PLANNED]`

#### Endpoint Summary

| Method | Path | Role | Description |
|--------|------|------|-------------|
| POST | `/api/v1/routes/calculate` | Admin | Calculate an optimal delivery route |
| GET | `/api/v1/routes` | Admin | List all delivery routes |
| GET | `/api/v1/routes/{routeId}` | Admin, Restaurant Manager/Staff (own deliveries only) | Get route detail |
| POST | `/api/v1/vehicles` | Admin | Register a vehicle |
| GET | `/api/v1/vehicles` | Admin | List all vehicles |

---

#### POST /api/v1/routes/calculate

**Role:** Admin only

Calculates an optimized delivery route for a set of orders using a nearest-neighbor heuristic with 2-opt improvement. Routes are cached for 1 hour (keyed by SHA-256 of sorted stop IDs and optimization criterion). Maximum 20 stops per route calculation; larger sets must be split.

**Request body:**

```json
{
  "orderIds": [
    "i9j0k1l2-m3n4-5678-opqr-901234567890",
    "m3n4o5p6-q7r8-9012-stuv-345678901234"
  ],
  "vehicleId": "p6q7r8s9-t0u1-2345-vwxy-678901234567",
  "routeType": "market_hub_restaurant",
  "optimizationCriteria": "cost"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `orderIds` | array of UUIDs | Yes | Non-empty, max 20 orders |
| `vehicleId` | UUID | Yes | Must reference an active, available vehicle |
| `routeType` | string | Yes | One of: `market_hub_restaurant`, `direct` |
| `optimizationCriteria` | string | No | One of: `distance`, `time`, `cost`. Default: `cost` |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "routeId": "q7r8s9t0-u1v2-3456-wxyz-789012345678",
    "routeType": "market_hub_restaurant",
    "optimizationCriteria": "cost",
    "vehicleId": "p6q7r8s9-t0u1-2345-vwxy-678901234567",
    "totalDistanceKm": 42.7,
    "estimatedDurationMinutes": 120,
    "estimatedCostVnd": 450000,
    "stops": [
      {
        "stopOrder": 1,
        "entityType": "market",
        "entityId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
        "entityName": "Chợ đầu mối Hóc Môn",
        "estimatedArrivalAt": "2026-05-10T02:00:00+07:00",
        "estimatedDepartureAt": "2026-05-10T02:30:00+07:00"
      },
      {
        "stopOrder": 2,
        "entityType": "hub",
        "entityId": "r8s9t0u1-v2w3-4567-xyza-890123456789",
        "entityName": "FFX Hub Tân Bình",
        "estimatedArrivalAt": "2026-05-10T03:15:00+07:00",
        "estimatedDepartureAt": "2026-05-10T03:30:00+07:00"
      },
      {
        "stopOrder": 3,
        "entityType": "restaurant",
        "entityId": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
        "entityName": "Nhà hàng Phở Bà Tư",
        "estimatedArrivalAt": "2026-05-10T04:00:00+07:00",
        "estimatedDepartureAt": null
      }
    ],
    "fromCache": false,
    "createdAt": "2026-05-09T22:05:00+07:00"
  }
}
```

`fromCache: true` is returned when the result was served from the Redis route cache.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `orderIds` is empty or `routeType` is invalid |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `VEHICLE_NOT_FOUND` | `vehicleId` does not reference an active vehicle |
| 422 Unprocessable Entity | `STOP_LIMIT_EXCEEDED` | More than 20 unique stops would be generated. Client must split the order set into smaller groups. |
| 422 Unprocessable Entity | `VEHICLE_INACTIVE` | The specified vehicle has `is_available = false` |
| 422 Unprocessable Entity | `ORDER_NOT_CONFIRMED` | One or more orders are not in a state eligible for route planning |

---

#### GET /api/v1/routes

**Role:** Admin only

Returns a paginated list of all calculated delivery routes.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `status` | string | No | Filter by route status: `planned`, `in_progress`, `completed`, `cancelled` |
| `vehicleId` | UUID | No | Filter by assigned vehicle |
| `from` | ISO 8601 date | No | Filter by `createdAt` start date |
| `to` | ISO 8601 date | No | Filter by `createdAt` end date |
| `page` | integer | No | Default: 1 |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "q7r8s9t0-u1v2-3456-wxyz-789012345678",
      "vehicleId": "p6q7r8s9-t0u1-2345-vwxy-678901234567",
      "vehiclePlate": "51C-12345",
      "status": "planned",
      "routeType": "market_hub_restaurant",
      "totalDistanceKm": 42.7,
      "estimatedDurationMinutes": 120,
      "stopCount": 3,
      "createdAt": "2026-05-09T22:05:00+07:00"
    }
  ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 5
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### GET /api/v1/routes/{routeId}

**Role:** Admin (any route), Restaurant Manager/Staff (only routes serving their member restaurants' orders)

Returns the full detail of a specific delivery route, including all stops.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `routeId` | UUID | The route to retrieve |

**Success response — 200 OK:**

Same structure as the route object returned from `POST /api/v1/routes/calculate`.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Restaurant user requesting a route that does not include any of their orders |
| 404 Not Found | `ROUTE_NOT_FOUND` | `routeId` does not exist |

---

#### POST /api/v1/vehicles

**Role:** Admin only

Registers a new vehicle in the system fleet.

**Request body:**

```json
{
  "plateNumber": "51C-12345",
  "capacityKg": 2000.00,
  "vehicleType": "VAN"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `plateNumber` | string | Yes | Non-empty, max 20 characters, must be unique |
| `capacityKg` | number | Yes | Must be > 0 |
| `vehicleType` | string | Yes | Non-empty, max 50 characters. Example values: `VAN`, `TRUCK`, `MOTORBIKE` |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "p6q7r8s9-t0u1-2345-vwxy-678901234567",
    "plateNumber": "51C-12345",
    "capacityKg": 2000.00,
    "vehicleType": "VAN",
    "isAvailable": true,
    "registeredBy": "admin-user-uuid",
    "createdAt": "2026-05-09T09:00:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Required field is missing or invalid |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 409 Conflict | `PLATE_NUMBER_EXISTS` | A vehicle with this plate number already exists |

---

#### GET /api/v1/vehicles

**Role:** Admin only

Returns a paginated list of all registered vehicles.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `isAvailable` | boolean | No | Filter by availability |
| `vehicleType` | string | No | Filter by vehicle type |
| `page` | integer | No | Default: 1 |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "p6q7r8s9-t0u1-2345-vwxy-678901234567",
      "plateNumber": "51C-12345",
      "capacityKg": 2000.00,
      "vehicleType": "VAN",
      "isAvailable": true,
      "createdAt": "2026-05-09T09:00:00+07:00"
    }
  ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 3
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

### 4.4 Hub Domain `[PLANNED]`

#### Endpoint Summary

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/api/v1/hubs` | Admin | List all active hubs |
| POST | `/api/v1/hubs` | Admin | Create a new hub |
| POST | `/api/v1/hubs/{hubId}/inbound` | Admin | Record inbound goods arriving at a hub |
| POST | `/api/v1/hubs/{hubId}/outbound` | Admin | Record outbound goods leaving a hub |
| GET | `/api/v1/hubs/{hubId}/inventory` | Admin | View current hub inventory |

---

#### GET /api/v1/hubs

**Role:** Admin only

Returns a list of all active hubs, including current capacity utilization.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `isActive` | boolean | No | Default: `true`. Set to `false` to include inactive hubs. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "r8s9t0u1-v2w3-4567-xyza-890123456789",
      "name": "FFX Hub Tân Bình",
      "address": "123 Đường Cộng Hòa, Quận Tân Bình, TP.HCM",
      "latitude": 10.8014,
      "longitude": 106.6520,
      "capacityKg": 5000.00,
      "occupiedCapacityKg": 1250.50,
      "availableCapacityKg": 3749.50,
      "utilizationPercent": 25.01,
      "isActive": true,
      "createdAt": "2026-01-01T07:00:00+07:00"
    }
  ]
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### POST /api/v1/hubs

**Role:** Admin only

Creates a new distribution hub.

**Request body:**

```json
{
  "name": "FFX Hub Bình Thạnh",
  "address": "456 Đường Xô Viết Nghệ Tĩnh, Quận Bình Thạnh, TP.HCM",
  "latitude": 10.8156,
  "longitude": 106.7081,
  "capacityKg": 3000.00
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `name` | string | Yes | Non-empty, max 200 characters |
| `address` | string | No | Max 500 characters |
| `latitude` | number | No | Between -90 and 90 |
| `longitude` | number | No | Between -180 and 180 |
| `capacityKg` | number | Yes | Must be > 0 |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "s9t0u1v2-w3x4-5678-yzab-901234567890",
    "name": "FFX Hub Bình Thạnh",
    "address": "456 Đường Xô Viết Nghệ Tĩnh, Quận Bình Thạnh, TP.HCM",
    "latitude": 10.8156,
    "longitude": 106.7081,
    "capacityKg": 3000.00,
    "occupiedCapacityKg": 0,
    "availableCapacityKg": 3000.00,
    "isActive": true,
    "createdAt": "2026-05-09T09:30:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Required field missing or invalid |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### POST /api/v1/hubs/{hubId}/inbound

**Role:** Admin only

Records goods arriving at a hub from a market. Updates `hub_inventory` transactionally. Returns an error if the incoming quantity would exceed the hub's storage capacity.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `hubId` | UUID | The hub receiving the goods |

**Request body:**

```json
{
  "sourceMarketId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
  "deliveryRouteId": "q7r8s9t0-u1v2-3456-wxyz-789012345678",
  "items": [
    {
      "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
      "quantityKg": 150.5
    },
    {
      "marketProductId": "h8i9j0k1-l2m3-4567-nopq-890123456789",
      "quantityKg": 80.0
    }
  ],
  "arrivedAt": "2026-05-10T03:15:00+07:00"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `sourceMarketId` | UUID | No | Must reference an active market if provided |
| `deliveryRouteId` | UUID | No | Must reference an existing delivery route if provided |
| `items` | array | Yes | Non-empty array of product/quantity pairs |
| `items[].marketProductId` | UUID | Yes | Must reference an active market product |
| `items[].quantityKg` | number | Yes | Must be > 0 |
| `arrivedAt` | ISO 8601 | No | Defaults to current server time if not provided |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "t0u1v2w3-x4y5-6789-zabc-012345678901",
    "hubId": "r8s9t0u1-v2w3-4567-xyza-890123456789",
    "sourceMarketId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
    "deliveryRouteId": "q7r8s9t0-u1v2-3456-wxyz-789012345678",
    "totalQuantityKg": 230.5,
    "itemCount": 2,
    "arrivedAt": "2026-05-10T03:15:00+07:00",
    "recordedBy": "admin-user-uuid",
    "createdAt": "2026-05-10T03:16:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Required field missing or `items` is empty |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `HUB_NOT_FOUND` | `hubId` does not reference an active hub |
| 409 Conflict | `ALREADY_RECEIVED` | This `deliveryRouteId` has already been recorded as inbound at this hub |
| 422 Unprocessable Entity | `HUB_CAPACITY_EXCEEDED` | Total incoming quantity would exceed hub's `capacityKg`. Response includes `currentOccupiedKg`, `capacityKg`, and `requestedKg`. |

---

#### POST /api/v1/hubs/{hubId}/outbound

**Role:** Admin only

Records goods departing from a hub toward a delivery route (restaurants). The hub must have sufficient stock of each product. Updates `hub_inventory` transactionally.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `hubId` | UUID | The hub from which goods are departing |

**Request body:**

```json
{
  "destinationRouteId": "q7r8s9t0-u1v2-3456-wxyz-789012345678",
  "items": [
    {
      "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
      "quantityKg": 100.0
    }
  ],
  "dispatchedAt": "2026-05-10T03:30:00+07:00"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `destinationRouteId` | UUID | No | Must reference an existing delivery route if provided |
| `items` | array | Yes | Non-empty array |
| `items[].marketProductId` | UUID | Yes | Must reference a product with stock in this hub |
| `items[].quantityKg` | number | Yes | Must be > 0 and must not exceed available hub stock for this product |
| `dispatchedAt` | ISO 8601 | No | Defaults to current server time |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "u1v2w3x4-y5z6-7890-abcd-123456789012",
    "hubId": "r8s9t0u1-v2w3-4567-xyza-890123456789",
    "destinationRouteId": "q7r8s9t0-u1v2-3456-wxyz-789012345678",
    "totalQuantityKg": 100.0,
    "itemCount": 1,
    "dispatchedAt": "2026-05-10T03:30:00+07:00",
    "recordedBy": "admin-user-uuid",
    "createdAt": "2026-05-10T03:31:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Required field missing or `items` is empty |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `HUB_NOT_FOUND` | `hubId` does not reference an active hub |
| 422 Unprocessable Entity | `INSUFFICIENT_HUB_STOCK` | One or more items exceed available hub stock. Response `details` lists each product with `requestedKg` and `availableKg`. |

---

#### GET /api/v1/hubs/{hubId}/inventory

**Role:** Admin only

Returns the current inventory state for a specific hub — all products with their available quantities.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `hubId` | UUID | The hub to query |

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `page` | integer | No | Default: 1 |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "hubId": "r8s9t0u1-v2w3-4567-xyza-890123456789",
    "hubName": "FFX Hub Tân Bình",
    "capacityKg": 5000.00,
    "occupiedCapacityKg": 1250.50,
    "inventory": [
      {
        "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
        "productName": "Cá lóc",
        "category": "thủy hải sản",
        "unit": "kg",
        "marketName": "Chợ đầu mối Hóc Môn",
        "quantityIn": 150.5,
        "quantityOut": 100.0,
        "quantityAvailable": 50.5,
        "lastUpdatedAt": "2026-05-10T03:31:00+07:00"
      }
    ]
  },
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 2
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `HUB_NOT_FOUND` | `hubId` does not reference an existing hub |

---

### 4.5 Analytics Domain `[PLANNED]`

#### Endpoint Summary

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/api/v1/analytics/price-trends` | Admin, Restaurant | Get price trend time-series data |
| GET | `/api/v1/analytics/demand-heatmap` | Admin | Get demand aggregation data |
| GET | `/api/v1/analytics/delivery-performance` | Admin | Get delivery KPI metrics |
| POST | `/api/v1/analytics/export` | Admin | Submit async CSV export job |
| GET | `/api/v1/analytics/export/{exportId}/status` | Admin | Poll export job status |

---

#### GET /api/v1/analytics/price-trends

**Role:** Admin, Restaurant Manager/Staff

Returns a price trend time-series for one or more products at one or more markets. Served from pre-aggregated cache (15-minute TTL). Supports `hourly` and `daily` intervals.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `marketProductId` | UUID | Yes | The market product to analyze. Can be repeated up to 10 times: `?marketProductId=uuid1&marketProductId=uuid2` |
| `from` | ISO 8601 date | Yes | Start of analysis period (inclusive) |
| `to` | ISO 8601 date | Yes | End of analysis period (inclusive) |
| `interval` | string | No | `hourly` or `daily`. Default: `daily`. Requests spanning more than 12 months automatically use `daily`. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "series": [
      {
        "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
        "productName": "Cá lóc",
        "marketName": "Chợ đầu mối Hóc Môn",
        "interval": "daily",
        "summary": {
          "minPrice": 110000.00,
          "maxPrice": 145000.00,
          "avgPrice": 126500.00,
          "priceVolatility": 8750.50
        },
        "dataPoints": [
          {
            "timestamp": "2026-05-01T00:00:00+07:00",
            "avgPrice": 120000.00,
            "minPrice": 115000.00,
            "maxPrice": 125000.00,
            "snapshotCount": 8
          },
          {
            "timestamp": "2026-05-02T00:00:00+07:00",
            "avgPrice": 125000.00,
            "minPrice": 120000.00,
            "maxPrice": 130000.00,
            "snapshotCount": 10
          }
        ]
      }
    ]
  }
}
```

`priceVolatility` is the standard deviation of price across the requested period.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `from` or `to` is missing or invalid; more than 10 `marketProductId` values provided |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is a Market Agent / Hub Staff / Driver |

---

#### GET /api/v1/analytics/demand-heatmap

**Role:** Admin only

Returns aggregated order demand by restaurant geographic location for a time period. Used to populate geographic heatmap visualizations.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `from` | ISO 8601 date | Yes | Start date |
| `to` | ISO 8601 date | Yes | End date |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "from": "2026-05-01T00:00:00+07:00",
    "to": "2026-05-09T23:59:59+07:00",
    "restaurants": [
      {
        "restaurantId": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
        "restaurantName": "Nhà hàng Phở Bà Tư",
        "latitude": 10.7769,
        "longitude": 106.7009,
        "totalOrderCount": 9,
        "totalOrderValueVnd": 62250000.00,
        "dominantProductCategory": "thủy hải sản"
      }
    ],
    "timeDistribution": {
      "hourlyMatrix": [
        { "dayOfWeek": 1, "hour": 4, "orderCount": 12 },
        { "dayOfWeek": 1, "hour": 5, "orderCount": 28 }
      ]
    }
  }
}
```

`dayOfWeek` is ISO 8601 weekday (1 = Monday, 7 = Sunday). `hour` is in `Asia/Ho_Chi_Minh` timezone (0–23).

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `from` or `to` missing or invalid |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### GET /api/v1/analytics/delivery-performance

**Role:** Admin only

Returns delivery KPI metrics for a specified time period. A delivery is classified as late if `actualArrival` exceeds the planned arrival (estimated by route calculation) by more than 15 minutes.

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `from` | ISO 8601 date | Yes | Start date |
| `to` | ISO 8601 date | Yes | End date |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "from": "2026-05-01T00:00:00+07:00",
    "to": "2026-05-09T23:59:59+07:00",
    "totalDeliveries": 45,
    "onTimeCount": 40,
    "lateCount": 5,
    "onTimeRatePercent": 88.89,
    "avgDeliveryDurationMinutes": 112,
    "avgVehicleUtilizationPercent": 73.4
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `from` or `to` missing or invalid |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### POST /api/v1/analytics/export

**Role:** Admin only

Submits an asynchronous CSV export job. For exports with fewer than 50,000 rows, the file may be ready within seconds. For large exports, the client polls the status endpoint until `status` is `ready`, then retrieves the file.

**Request body:**

```json
{
  "type": "price_history",
  "from": "2026-04-01",
  "to": "2026-05-09"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `type` | string | Yes | One of: `price_history`, `orders`, `delivery` |
| `from` | ISO 8601 date | Yes | Start date of export range |
| `to` | ISO 8601 date | Yes | End date of export range. Must not be before `from`. |

**Success response — 202 Accepted:**

```json
{
  "success": true,
  "data": {
    "exportId": "v2w3x4y5-z6a7-8901-bcde-234567890123",
    "status": "pending",
    "type": "price_history",
    "from": "2026-04-01",
    "to": "2026-05-09",
    "createdAt": "2026-05-09T12:00:00+07:00",
    "estimatedReadyAt": "2026-05-09T12:00:30+07:00"
  }
}
```

The file is available for download for 24 hours after it becomes `ready`, after which it is purged from storage.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Required field missing, invalid `type`, or `to` is before `from` |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### GET /api/v1/analytics/export/{exportId}/status

**Role:** Admin only

Polls the status of an async export job. When `status` is `ready`, a `downloadUrl` is included in the response for the client to retrieve the CSV file directly.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `exportId` | UUID | The export job ID returned from `POST /api/v1/analytics/export` |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "exportId": "v2w3x4y5-z6a7-8901-bcde-234567890123",
    "status": "ready",
    "type": "price_history",
    "from": "2026-04-01",
    "to": "2026-05-09",
    "rowCount": 12450,
    "readyAt": "2026-05-09T12:00:28+07:00",
    "expiresAt": "2026-05-10T12:00:28+07:00",
    "downloadUrl": "https://api.freshflow.vn/api/v1/analytics/export/v2w3x4y5-z6a7-8901-bcde-234567890123/download"
  }
}
```

`status` values: `pending`, `processing`, `ready`, `failed`. `downloadUrl` is only present when `status = ready`.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin or does not own this export job |
| 404 Not Found | `EXPORT_NOT_FOUND` | `exportId` does not exist or has expired |

---

### 4.6 Admin Domain

#### Endpoint Summary

| Method | Path | Role | Description |
|--------|------|------|-------------|
| GET | `/api/v1/admin/roles` | Admin | List seeded global roles |
| POST | `/api/v1/admin/users` | Admin | Create a user account and optional role-specific assignment |
| GET | `/api/v1/admin/users` | Admin | List users |
| PATCH | `/api/v1/admin/users/{userId}/status` | Admin | Suspend/reactivate/delete a user account |
| PATCH | `/api/v1/admin/users/{userId}/role` | Admin | Change one user's global role |
| POST | `/api/v1/admin/users/{userId}/unlock` | Admin | Clear temporary account lock after failed logins |
| PUT | `/api/v1/admin/users/{userId}/market-assignments` | Admin | Replace Market Agent market assignments |
| PATCH | `/api/v1/admin/restaurants/{restaurantId}/approve` | Admin | Approve a restaurant profile |
| GET | `/api/v1/admin/system-config` | Admin | View system configuration |
| PATCH | `/api/v1/admin/system-config` | Admin | Update system configuration |

---

#### GET /api/v1/admin/roles

**Role:** Admin only

Returns all active roles from the `roles` table. Roles are seeded during deployment; v1 does not expose role creation or permission editing because endpoint-to-role policies are code-defined and documented in the RBAC matrix.

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "admin",
      "description": "System administrator",
      "isActive": true
    },
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "name": "market_agent",
      "description": "Updates prices, procures goods, and hands off to hub",
      "isActive": true
    }
  ]
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### POST /api/v1/admin/users

**Role:** Admin only

Creates a user account with exactly one global role. Restaurant owners may self-register via `POST /api/v1/auth/register` (UC-AUTH-11); all other roles (`market_agent`, `hub_staff`, `driver`) are Admin-managed. The endpoint can also create role-specific associations:

- For `market_agent` / legacy request alias `kiosk_staff`, `marketId` creates a row in `user_market_assignments`.
- For `driver`, the system creates a row in `driver_profiles`.
- For `restaurant`, `restaurantName` creates a pending restaurant profile.

**Request header:** `Authorization: Bearer <adminAccessToken>`

**Request body:**

```json
{
  "email": "staff.hocmon@freshflow.vn",
  "password": "TempP@ssw0rd!",
  "role": "market_agent",
  "marketId": "b2c3d4e5-f6a7-8901-bcde-f01234567890",
  "restaurantName": null,
  "phone": "+84901234567"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `email` | string | Yes | Valid email format, max 255 characters, must be unique |
| `password` | string | Yes | Must satisfy password strength policy |
| `role` | string | Yes | One seeded lowercase `roles.name` value; `kiosk_staff` is accepted as an alias for `market_agent` |
| `marketId` | UUID | Conditional | Required for `market_agent` / `kiosk_staff` |
| `restaurantName` | string | Conditional | Required for `restaurant` |
| `phone` | string | No | 7-15 digits with optional leading `+`; must be unique when provided |

**Success response — 201 Created:**

```json
{
  "success": true,
  "data": {
    "id": "c4d5e6f7-a8b9-0123-cdef-012345678901",
    "email": "staff.hocmon@freshflow.vn",
    "role": "market_agent",
    "isActive": true,
    "createdAt": "2026-05-09T10:30:00+07:00"
  }
}
```

When a `restaurant` user is created, its restaurant profile starts unapproved; restaurant users can log in but cannot place orders until the restaurant is approved.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | Any required field is missing or fails format validation |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid access token |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 409 Conflict | `EMAIL_ALREADY_EXISTS` | An account with this email address already exists |
| 409 Conflict | `PHONE_ALREADY_EXISTS` | An account with this phone number already exists |
| 422 Unprocessable Entity | `INVALID_ROLE` | `role` does not reference an active seeded role |
| 422 Unprocessable Entity | `INVALID_MARKET` | `marketId` does not reference an active market |

---

#### GET /api/v1/admin/users

**Role:** Admin only

Returns a paginated list of all user accounts (active and inactive, all roles).

**Query parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `role` | string | No | Filter by lowercase role name, e.g. `market_agent` |
| `isActive` | boolean | No | Filter by active/deactivated account state |
| `search` | string | No | Filter by email or phone |
| `page` | integer | No | Default: 1 |
| `pageSize` | integer | No | Default: 20. Max: 100. |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": [
	    {
      "id": "c4d5e6f7-a8b9-0123-cdef-012345678901",
      "email": "staff.hocmon@freshflow.vn",
      "role": "market_agent",
      "isActive": true,
      "isApproved": null,
      "createdAt": "2026-05-09T10:30:00+07:00"
    },
    {
      "id": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
      "email": "manager@phobaatu.vn",
      "role": "restaurant",
      "isActive": true,
      "isApproved": false,
      "createdAt": "2026-05-08T14:00:00+07:00"
    }
  ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 12
  }
}
```

`isApproved` is populated only for restaurant users.

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### PATCH /api/v1/admin/users/{userId}/status `[CHANGED]`

> `[CHANGED]` Implemented as `PATCH /api/v1/admin/users/{userId}/activate` (see Part A §A3).

**Role:** Admin only

Updates `users.status`. Setting status to `SUSPENDED` or `DELETED` prevents new login and revokes all refresh tokens for the user. Existing access tokens remain valid only until their 15-minute TTL expires.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `userId` | UUID | The user account to update |

**Request body:**

```json
{
  "status": "SUSPENDED"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `status` | string | Yes | One of `ACTIVE`, `SUSPENDED`, `DELETED` |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "id": "c4d5e6f7-a8b9-0123-cdef-012345678901",
    "email": "staff.hocmon@freshflow.vn",
    "role": "market_agent",
    "status": "SUSPENDED",
    "updatedAt": "2026-05-09T14:00:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `status` field is missing or invalid |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `USER_NOT_FOUND` | `userId` does not exist |
| 422 Unprocessable Entity | `CANNOT_DISABLE_SELF` | Admin attempting to suspend/delete their own account |

---

#### PATCH /api/v1/admin/users/{userId}/role

**Role:** Admin only

Changes one user's global role by updating `users.role_id`. This is the RBAC administration endpoint for UC-AUTH-10. The system revokes all refresh tokens for the user after a role change so the next login/refresh receives JWT claims for the new role.

**Request body:**

```json
{
  "roleName": "hub_staff"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `roleName` | string | Yes | Must match an active lowercase row in `roles.name` |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "id": "c4d5e6f7-a8b9-0123-cdef-012345678901",
    "email": "staff.hocmon@freshflow.vn",
    "role": "hub_staff",
    "updatedAt": "2026-05-09T14:15:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `roleName` is missing |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `USER_NOT_FOUND` | `userId` does not exist |
| 422 Unprocessable Entity | `INVALID_ROLE` | Role does not exist or is inactive |
| 422 Unprocessable Entity | `CANNOT_CHANGE_OWN_ROLE` | Admin attempting to change their own role |

---

#### POST /api/v1/admin/users/{userId}/unlock

**Role:** Admin only

Clears a temporary account lock after failed logins by setting `users.failed_login_count = 0` and `users.locked_until = null`.

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "id": "c4d5e6f7-a8b9-0123-cdef-012345678901",
    "failedLoginCount": 0,
    "lockedUntil": null,
    "updatedAt": "2026-05-09T14:20:00+07:00"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `USER_NOT_FOUND` | `userId` does not exist |

---

#### PUT /api/v1/admin/users/{userId}/market-assignments

**Role:** Admin only

Replaces a Market Agent user's market assignments in `user_market_assignments`. This endpoint is required for market-level authorization on pricing updates.

**Request body:**

```json
{
  "marketIds": [
    "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
    "b2c3d4e5-f6a7-8901-bcde-f01234567890"
  ]
}
```

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "userId": "c4d5e6f7-a8b9-0123-cdef-012345678901",
    "marketAssignments": [
      {
        "marketId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
        "marketName": "Cho dau moi Hoc Mon"
      }
    ]
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | `marketIds` is missing or empty |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `USER_NOT_FOUND` | `userId` does not exist |
| 422 Unprocessable Entity | `INVALID_ROLE` | User is not a `market_agent` or legacy alias `kiosk_staff` |
| 422 Unprocessable Entity | `INVALID_MARKET` | One or more market IDs are invalid/inactive |

---

#### PATCH /api/v1/admin/restaurants/{restaurantId}/approve

**Role:** Admin only

Approves a restaurant profile by changing `restaurants.status` from `PENDING_APPROVAL` to `ACTIVE`, allowing active restaurant members to place orders.

**Path parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `restaurantId` | UUID | The restaurant (not user) ID to approve |

**Request body:** None required (approval is a single action).

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "restaurantId": "j0k1l2m3-n4o5-6789-pqrs-012345678901",
    "restaurantName": "Nhà hàng Phở Bà Tư",
    "status": "ACTIVE",
    "approvedAt": "2026-05-09T14:30:00+07:00",
    "approvedBy": "admin-user-uuid"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |
| 404 Not Found | `RESTAURANT_NOT_FOUND` | `restaurantId` does not exist |
| 409 Conflict | `ALREADY_APPROVED` | The restaurant status is already `ACTIVE` |

---

#### GET /api/v1/admin/system-config `[PLANNED]`

**Role:** Admin only

Returns all system-wide configurable parameters.

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "dailyOrderCutoffTime": "22:00",
    "priceBandTolerancePercent": 10.00,
    "softReservationWindowMinutes": 30,
    "updatedAt": "2026-05-09T07:00:00+07:00",
    "updatedBy": "admin-user-uuid"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

#### PATCH /api/v1/admin/system-config `[PLANNED]`

**Role:** Admin only

Updates one or more system configuration values. Only the provided fields are updated (partial update semantics).

**Request body:**

```json
{
  "dailyOrderCutoffTime": "22:00",
  "priceBandTolerancePercent": 10.00
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `dailyOrderCutoffTime` | string (`HH:mm`) | No | Local time in `Asia/Ho_Chi_Minh`; default `22:00` |
| `priceBandTolerancePercent` | number | No | Must be between 0.00 and 50.00 (inclusive); default `10.00` |
| `softReservationWindowMinutes` | integer | No | Must be between 5 and 120 (inclusive) |

**Success response — 200 OK:**

```json
{
  "success": true,
  "data": {
    "dailyOrderCutoffTime": "22:00",
    "priceBandTolerancePercent": 10.00,
    "softReservationWindowMinutes": 30,
    "updatedAt": "2026-05-09T15:00:00+07:00",
    "updatedBy": "admin-user-uuid"
  }
}
```

**Error responses:**

| Status | Error Code | Condition |
|--------|-----------|-----------|
| 400 Bad Request | `VALIDATION_ERROR` | A provided field fails its validation rule |
| 401 Unauthorized | `UNAUTHORIZED` | Missing or invalid JWT |
| 403 Forbidden | `FORBIDDEN` | Authenticated user is not an Admin |

---

## 5. SignalR Hubs

### 5.1 Overview and Authentication

All three SignalR hubs require JWT authentication. Because the HTTP `Authorization` header is not accessible during the WebSocket upgrade handshake in browsers, the JWT is passed as a query string parameter during the SignalR negotiate request:

```
wss://api.freshflow.vn/hubs/pricing?access_token=<accessToken>
```

The server validates the JWT at the negotiate step. If the token is invalid or expired, the negotiate request returns HTTP 401 and the connection is rejected.

**Token expiry during an active connection:** The JWT is validated only at negotiate time. An existing WebSocket connection remains alive even if the access token expires during the session. Clients must proactively refresh the access token before the connection closes to avoid a gap. Clients that implement automatic reconnect must obtain a fresh access token before re-establishing the connection.

**Reconnection policy (mandatory on all clients):** Exponential backoff — initial delay 1 second, multiplier 2x, maximum delay 30 seconds, retry indefinitely until explicit logout. On reconnect, clients must re-join groups and re-fetch current state via REST (missed events are not replayed).

---

### 5.2 PricingHub

**Route:** `/hubs/pricing`

**Authentication:** JWT Bearer required. Validated on negotiate.

#### Groups

| Group Name Pattern | Who Joins | How |
|-------------------|-----------|-----|
| `market:{marketId}` | Restaurant and Admin clients, for each market they want to monitor | Client calls `SubscribeToMarket(marketId)` after connecting |
| `agent:{marketId}` | Market Agent clients, automatically scoped to assigned markets | Server joins on connect, validated against `user_market_assignments` |

Market Agents are server-side restricted. They may update or listen only for markets in `user_market_assignments`. A Market Agent calling `SubscribeToMarket` for an unassigned market receives an error response from the hub method.

#### Client → Server Methods

| Method | Parameters | Description |
|--------|------------|-------------|
| `SubscribeToMarket` | `marketId: string` | Adds the client connection to the `market:{marketId}` group. Must be called after connection is established. Restaurant, Admin, Operations Manager, and assigned Market Agent roles only. |
| `UnsubscribeFromMarket` | `marketId: string` | Removes the client connection from the `market:{marketId}` group. |

#### Server → Client Events

**`PriceUpdated`**

Broadcast to the `market:{marketId}` group after any price or quantity update by a Market Agent.

```json
{
  "marketId": "a1b2c3d4-e5f6-7890-abcd-ef0123456789",
  "productId": "e5f6a7b8-c9d0-1234-efab-234567890123",
  "marketProductId": "d4e5f6a7-b8c9-0123-defa-123456789012",
  "newPrice": 135000.00,
  "newQuantity": 420,
  "previousPrice": 125000.00,
  "changePercent": 8.00,
  "updatedAt": "2026-05-09T04:10:00+07:00"
}
```

`PriceUpdated` is sent within 500 ms of the database write completing.

#### Connection Lifecycle

| Event | Server Behavior | Client Responsibility |
|-------|----------------|----------------------|
| Connect | JWT validated. `connectionId` recorded. Market Agents are automatically joined to `agent:{assignedMarketId}` for each assignment. | Call `SubscribeToMarket(marketId)` for each market to monitor. |
| `SubscribeToMarket` called | Server calls `Groups.AddToGroupAsync(connectionId, "market:{marketId}")`. | Await confirmation before considering subscription active. |
| `UnsubscribeFromMarket` called | Server calls `Groups.RemoveFromGroupAsync(connectionId, "market:{marketId}")`. | Update local subscription state. |
| Disconnect (clean or network drop) | SignalR removes `connectionId` from all groups automatically. | Reconnect with exponential backoff. Re-subscribe to all markets. Re-fetch current state via `GET /api/v1/markets/{marketId}/products`. |

---

### 5.3 OrderHub

**Route:** `/hubs/orders`

**Authentication:** JWT Bearer required.

#### Groups

| Group Name Pattern | Who Joins | How |
|-------------------|-----------|-----|
| `restaurant:{restaurantId}` | Restaurant clients | Automatically on connect for each active `restaurant_members` row belonging to the user |
| `admin:orders` | Admin and Operations Manager clients | Automatically on connect — validated from JWT `role` claim |

Group join is fully automatic based on JWT claims. Clients do not need to invoke any hub methods to join their group.

#### Client → Server Methods

None. Order management is handled entirely via REST endpoints.

#### Server → Client Events

**`OrderStatusChanged`**

Broadcast to `restaurant:{restaurantId}` whenever an order belonging to that restaurant changes status.

```json
{
  "orderId": "i9j0k1l2-m3n4-5678-opqr-901234567890",
  "previousStatus": "confirmed",
  "newStatus": "in_transit",
  "updatedAt": "2026-05-10T04:05:00+07:00"
}
```

Also broadcast to `admin:orders` for Admin monitoring.

**`OrderGrouped`**

Broadcast to `restaurant:{restaurantId}` when one of the restaurant's orders is added to an order group.

```json
{
  "orderId": "i9j0k1l2-m3n4-5678-opqr-901234567890",
  "orderGroupId": "l2m3n4o5-p6q7-8901-rstu-234567890123",
  "groupName": "Nhóm giao hàng sáng 10/5",
  "updatedAt": "2026-05-09T22:00:00+07:00"
}
```

#### Connection Lifecycle

| Event | Server Behavior | Client Responsibility |
|-------|----------------|----------------------|
| Connect | JWT validated. `connectionId` recorded. Server auto-joins restaurant users to each active restaurant membership and Admin/Operations Manager users to `admin:orders`. | No manual group join required. |
| Disconnect | SignalR removes from all groups automatically. | Reconnect with exponential backoff. Re-fetch order status via `GET /api/v1/orders/{orderId}`. |

---

### 5.4 DeliveryHub `[PLANNED]`

**Route:** `/hubs/delivery`

**Authentication:** JWT Bearer required.

#### Groups

| Group Name Pattern | Who Joins | How |
|-------------------|-----------|-----|
| `restaurant:{restaurantId}` | Restaurant clients | Automatically on connect for each active `restaurant_members` row belonging to the user |
| `admin:delivery` | Admin and Operations Manager clients | Automatically on connect |

#### Client → Server Methods

None. Delivery management is handled entirely via REST endpoints.

#### Server → Client Events

**`DeliveryStatusChanged`**

Broadcast to `restaurant:{restaurantId}` when a delivery leg associated with one of their orders changes status.

```json
{
  "deliveryId": "w3x4y5z6-a7b8-9012-cdef-345678901234",
  "orderId": "i9j0k1l2-m3n4-5678-opqr-901234567890",
  "previousStatus": "pending",
  "newStatus": "in_transit",
  "estimatedArrival": "2026-05-10T04:00:00+07:00",
  "updatedAt": "2026-05-10T03:31:00+07:00"
}
```

In multi-drop routes, each restaurant receives only the `DeliveryStatusChanged` event for their own orders — not for other restaurants' orders on the same route.

**`RouteOptimized`**

Broadcast to the `admin:delivery` group when a new route is calculated and persisted.

```json
{
  "routeId": "q7r8s9t0-u1v2-3456-wxyz-789012345678",
  "vehicleId": "p6q7r8s9-t0u1-2345-vwxy-678901234567",
  "vehiclePlate": "51C-12345",
  "totalStops": 3,
  "estimatedDurationMinutes": 120,
  "createdAt": "2026-05-09T22:05:00+07:00"
}
```

#### Connection Lifecycle

| Event | Server Behavior | Client Responsibility |
|-------|----------------|----------------------|
| Connect | JWT validated. Auto-joins restaurant users to each active restaurant membership and Admin/Operations Manager users to `admin:delivery`. | No manual group join required. |
| Disconnect | SignalR removes from all groups automatically. | Reconnect with exponential backoff. Re-fetch delivery state via `GET /api/v1/routes/{routeId}`. |

---

## 6. Validation Rules

The following table defines all validation rules enforced by the API. Validation errors return HTTP 400 or HTTP 422 with the specified error code and field-level details.

| Field | Rule | HTTP Status | Error Code | Error Message |
|-------|------|-------------|-----------|---------------|
| `email` | Must be a valid email format | 400 | `VALIDATION_ERROR` | "Must be a valid email address" |
| `email` | Maximum 255 characters | 400 | `VALIDATION_ERROR` | "Must not exceed 255 characters" |
| `password` (registration) | Minimum 8 characters | 400 | `VALIDATION_ERROR` | "Must be at least 8 characters" |
| `password` (registration) | At least 1 uppercase letter | 400 | `VALIDATION_ERROR` | "Must contain at least one uppercase letter" |
| `password` (registration) | At least 1 digit | 400 | `VALIDATION_ERROR` | "Must contain at least one number" |
| `password` (registration) | At least 1 special character | 400 | `VALIDATION_ERROR` | "Must contain at least one special character" |
| `price` | Must be a numeric value greater than 0 | 422 | `VALIDATION_ERROR` | "Price must be greater than 0" |
| `price` | Maximum 2 decimal places | 422 | `VALIDATION_ERROR` | "Price must have at most 2 decimal places" |
| `quantity` (kiosk update) | Must be an integer | 422 | `VALIDATION_ERROR` | "Quantity must be a whole number" |
| `quantity` (kiosk update) | Must be >= 0 | 422 | `VALIDATION_ERROR` | "Quantity must be 0 or greater" |
| `quantity` (order item) | Must be an integer | 422 | `VALIDATION_ERROR` | "Quantity must be a whole number" |
| `quantity` (order item) | Must be > 0 | 422 | `VALIDATION_ERROR` | "Quantity must be greater than 0" |
| `scheduledFor` | If provided, must be at least 2 hours in the future | 422 | `BUSINESS_RULE_ERROR` | "Scheduled time must be at least 2 hours from now" |
| `orderIds` (route calculation) | Must not be empty | 400 | `VALIDATION_ERROR` | "At least one order must be provided" |
| `orderIds` (route calculation) | Maximum 20 orders | 422 | `VALIDATION_ERROR` | "Cannot calculate a route for more than 20 orders at once" |
| Order cancellation | Order status must be `pending` or `confirmed` | 409 | `BUSINESS_RULE_ERROR` | "Order cannot be cancelled in its current status" |
| Kiosk market assignment | Staff member must be assigned to the target market | 403 | `AUTHORIZATION_ERROR` | "You are not authorized to update prices at this market" |
| Restaurant approval | Unapproved restaurant attempting to place an order | 422 | `BUSINESS_RULE_ERROR` | "Your restaurant account is pending Admin approval" |
| `orderIds` (order group) | All orders must have status `confirmed` | 422 | `BUSINESS_RULE_ERROR` | "All orders must be confirmed before grouping" |
| `orderIds` (order group) | Orders must not already belong to an active group | 409 | `BUSINESS_RULE_ERROR` | "One or more orders are already in an active order group" |
| Auto-batch trigger | Another auto-batch run must not be active for the same target date | 409 | `BUSINESS_RULE_ERROR` | "Auto-batch is already running" |
| `items[].quantityKg` (hub outbound) | Must not exceed available hub stock | 422 | `BUSINESS_RULE_ERROR` | "Requested quantity exceeds available hub stock" |
| Export `to` date | Must not be before `from` date | 400 | `VALIDATION_ERROR` | "End date must be on or after start date" |
| `capacityKg` (vehicle/hub) | Must be > 0 | 400 | `VALIDATION_ERROR` | "Capacity must be greater than 0" |
| JWT in request | Must be present and non-expired on all protected endpoints | 401 | `UNAUTHORIZED` | "Authentication is required" |
| Role claim | Role must match the endpoint's access policy | 403 | `FORBIDDEN` | "You do not have permission to perform this action" |

---

## 7. API Security

### 7.1 RBAC Matrix

The following table shows which roles can access each endpoint group. A checkmark (✓) means the role has access; a cross (✗) means access is denied with HTTP 403. The legacy request value `kiosk_staff` is treated as an alias of `market_agent` wherever Market Agent access is listed.

Role columns: `Admin` = `admin`, `Ops` = `operations_manager`, `Agent` = `market_agent`/`kiosk_staff`, `Hub` = `hub_staff`, `Driver` = `driver`, `Restaurant` = `restaurant`.

| Endpoint Group | Admin | Ops | Agent | Hub | Driver | RMgr | RStaff | Public |
|---------------|-------|-----|-------|-----|--------|------|--------|--------|
| `POST /auth/login` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `POST /auth/refresh` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `POST /auth/logout` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| `POST /auth/change-password` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| `POST /auth/forgot-password` / `reset-password` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `POST /auth/verify/request` / `verify` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `GET /admin/roles` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `POST /admin/users` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET /admin/users` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `PATCH /admin/users/{id}/status` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `PATCH /admin/users/{id}/role` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `POST /admin/users/{id}/unlock` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `PUT /admin/users/{id}/market-assignments` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `PATCH /admin/restaurants/{id}/approve` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET/PATCH /admin/system-config` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET /markets` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| `GET /markets/{id}/products` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| `GET /markets/{id}/products/{id}/price-history` | ✓ | ✓ | ✓ | ✗ | ✗ | ✓ | ✓ | ✗ |
| `PATCH /markets/{id}/products/{id}/price` | ✗ | ✗ | ✓ (assigned market only) | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET /products` | ✓ | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ | ✗ |
| `POST /products` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `POST /orders` | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ (member restaurant) | ✓ (member restaurant) | ✗ |
| `GET /orders` | ✓ (all) | ✓ (all) | ✗ | ✗ | ✗ | ✓ (member restaurant) | ✓ (member restaurant) | ✗ |
| `GET /orders/{id}` | ✓ | ✓ | ✗ | ✓ (assigned hub flow) | ✓ (assigned delivery) | ✓ (member restaurant) | ✓ (member restaurant) | ✗ |
| `PATCH /orders/{id}/cancel` | ✓ | ✓ | ✗ | ✗ | ✗ | ✓ (member restaurant) | ✓ (member restaurant) | ✗ |
| `PATCH /orders/{id}/status` | ✓ | ✓ | ✗ | ✓ (hub transitions only) | ✓ (delivery transitions only) | ✗ | ✗ | ✗ |
| `GET /order-groups` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `POST /order-groups` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `POST /admin/order-groups/auto-batch` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET/POST /orders/scheduled` | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ (member restaurant) | ✓ (member restaurant) | ✗ |
| `POST /routes/calculate` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET /routes` | ✓ | ✓ | ✗ | ✗ | ✓ (assigned routes) | ✗ | ✗ | ✗ |
| `GET /routes/{id}` | ✓ | ✓ | ✗ | ✗ | ✓ (assigned route) | ✓ (own deliveries) | ✓ (own deliveries) | ✗ |
| `POST /vehicles` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET /vehicles` | ✓ | ✓ | ✗ | ✗ | ✓ (own/current vehicle) | ✗ | ✗ | ✗ |
| `GET /hubs` | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |
| `POST /hubs` | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `POST /hubs/{id}/inbound` | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |
| `POST /hubs/{id}/outbound` | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |
| `GET /hubs/{id}/inventory` | ✓ | ✓ | ✗ | ✓ | ✗ | ✗ | ✗ | ✗ |
| `GET /analytics/price-trends` | ✓ | ✓ | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ |
| `GET /analytics/demand-heatmap` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET /analytics/delivery-performance` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `POST /analytics/export` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| `GET /analytics/export/{id}/status` | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| SignalR `/hubs/pricing` | ✓ | ✓ | ✓ | ✗ | ✗ | ✓ | ✓ | ✗ |
| SignalR `/hubs/orders` | ✓ | ✓ | ✗ | ✗ | ✗ | ✓ | ✓ | ✗ |
| SignalR `/hubs/delivery` | ✓ | ✓ | ✗ | ✗ | ✓ | ✓ | ✓ | ✗ |

### 7.2 Rate Limiting Rules

All rate limits are enforced via Redis counters (`rate_limit:{userId}:{endpoint}` for authenticated endpoints, `rate_limit:ip:{hashedIp}:{endpoint}` for unauthenticated endpoints). When a limit is exceeded, the server returns **HTTP 429 Too Many Requests** with a `Retry-After` header indicating the number of seconds until the window resets.

| Endpoint | Limit | Scope | Notes |
|----------|-------|-------|-------|
| `PATCH /markets/*/products/*/price` | 10 requests/second | Per Market Agent user | Price updates are frequent during market opening hours (02:00–06:00 HCM). High limit reflects operational reality. |
| `POST /orders` | 5 requests/minute | Per Restaurant Manager/Staff user | Prevents accidental duplicate order submission. |
| `POST /routes/calculate` | 2 requests/minute | Per Admin user | Route calculation invokes the VRP solver; computationally expensive. |
| All read endpoints (GET) | 100 requests/second | Per authenticated user | Broad rate to protect against scraping while allowing dashboard polling. |
| `POST /auth/login` | 10 requests/minute | Per IP address | Brute force protection on the login endpoint. |
| `POST /auth/refresh` | 10 requests/minute | Per IP address | Protects the refresh endpoint from token enumeration attacks. |
| `POST /analytics/export` | 5 requests/minute | Per Admin user | Prevents abuse of the export job queue. |

**HTTP 429 response format:**

```json
{
  "success": false,
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Too many requests. Please wait before retrying.",
    "details": [
      { "field": "retryAfterSeconds", "message": "45" }
    ]
  }
}
```

Response headers included with 429:
```
Retry-After: 45
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1746768000
```

### 7.3 Resource-Level Authorization

Resource-level authorization is enforced in the application service layer, not solely at the controller level.

**Restaurant isolation:** Restaurant Manager/Staff users can only read and modify data for restaurants where they have an active `restaurant_members` row. The JWT `sub` claim is the user ID, not the restaurant ID. For every restaurant-scoped operation, the service resolves active restaurant memberships for `users.id = sub` and checks the target `restaurant_id` against that set. Access to another restaurant returns HTTP 403.

**Market Agent market scope:** Market Agents can only update prices and quantities for markets in `user_market_assignments`. The market assignment check is performed by `PricingService` on every `PATCH /markets/{marketId}/products/{productId}/price` request by looking up the authenticated user's ID in `user_market_assignments` for the requested `marketId`. A user not assigned to the market receives HTTP 403 with error code `MARKET_ACCESS_DENIED`.

**Admin and Operations Manager access:** Admin users bypass resource-level ownership checks and can access all orders, routes, hubs, and user accounts. Operations Manager users can access operational order/logistics/hub views where listed in the RBAC matrix, but cannot manage users, roles, or system configuration. Policy names use lowercase role names from `roles.name`, e.g. `[Authorize(Roles = "admin")]`.

**Restaurant cross-data isolation:** A Restaurant Manager/Staff user cannot access any data belonging to restaurants outside their membership set. This includes orders, scheduled orders, invoices, delivery details, and SignalR groups. Query handlers must include membership-derived restaurant IDs in their predicates, e.g. `WHERE restaurant_id = ANY(@authorizedRestaurantIds)`.

**SignalR group authorization:** The `HubAuthorizationFilter` validates group membership at the SignalR hub level. Restaurant clients are joined only to `restaurant:{restaurantId}` groups from active `restaurant_members` rows. Market Agent clients are joined only to `agent:{marketId}` groups from `user_market_assignments`. Clients cannot join another restaurant's personal notification group or an unassigned market agent group.

---

*End of FreshFlow API Design Document v1.0*

*Prepared by: API Design Agent | Project: FFX Capstone 2026*
