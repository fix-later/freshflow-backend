# Enum and Status Values

Every enum-like value the API exposes, with the exact string a client will see.
**Reconciled with code:** 2026-08-22 (commit `bc5f1a4`).

> **How these reach the wire.** Response DTOs expose these fields as `string`, and EF stores
> them with `HasConversion<string>()`. The value on the wire is therefore the **C# member name
> verbatim** — which is why casing differs between modules. Do not normalize it; match it.

---

## 1. Roles

Source: `FreshFlow.Auth.Domain.Enums.RoleNames` and the `roles` table
(seeded by `20260606152229_AddRolesTable.cs`). These six are the only roles that exist.

| Value | Meaning |
|---|---|
| `admin` | System administrator |
| `operations_manager` | Operations manager |
| `market_agent` | Market agent (buys at the market) |
| `restaurant` | Restaurant user |
| `hub_staff` | Hub staff |
| `driver` | Driver |

> `docs/04` once cited `restaurant_manager` / `restaurant_staff` — **neither exists**.
> `[Authorize(Roles = "…")]` with an unknown name fails silently: green build, permanent 403.

```ts
export const RoleName = {
  Admin: 'admin',
  OperationsManager: 'operations_manager',
  MarketAgent: 'market_agent',
  Restaurant: 'restaurant',
  HubStaff: 'hub_staff',
  Driver: 'driver',
} as const;
export type RoleName = (typeof RoleName)[keyof typeof RoleName];
```

### 1.1 AdminCreateUserRole

`POST /api/v1/admin/users` accepts these values:

| Request value | Notes |
|---|---|
| `market_agent` | |
| `kiosk_staff` | Legacy alias; the backend stores and returns `market_agent` |
| `hub_staff` | |
| `driver` | |
| `restaurant` | |

---

## 2. Orders

Source: `FreshFlow.Orders.Domain.Enums`. **PascalCase.**

| Enum | Values |
|---|---|
| `OrderStatus` | `Draft`, `Confirmed`, `Batched`, `PickedUp`, `AtHub`, `Delivering`, `Delivered`, `Cancelled` |
| `OrderPaymentStatus` | `NotApplicable`, `Outstanding`, `Settled`, `Waived` |
| `OrderIssueType` | `Missing`, `Wrong`, `Damaged` |
| `OrderIssueStatus` | `Open`, `Resolved` |
| `OrderClaimStatus` | `Submitted`, `Approved`, `Rejected` |
| `RecurrenceType` | `Daily`, `Weekly` |
| `CreditTransactionType` | `Charge`, `Settlement`, `Refund`, `Adjustment` |
| `CreditAlertLevel` | `None`, `Warning`, `Exceeded` |
| `PaymentMethod` | `BankTransfer`, `Manual` |

> The order lifecycle is `Draft → Confirmed → Batched → PickedUp → AtHub → Delivering →
> Delivered`, with `Cancelled` reachable from `Draft` and `Confirmed` only.

---

## 3. Restaurants (Auth)

| Enum | Values | Casing on the wire |
|---|---|---|
| `RestaurantStatus` | `Pending`, `Active`, `Suspended` | **lowercase** in `restaurants.status` (`pending` / `active` / `suspended`) |

This is the one place where the stored value and the C# member differ in casing — check the
endpoint's schema before comparing strings.

---

## 4. Procurement

Source: `FreshFlow.Procurement.Domain.Enums`. **PascalCase.**

| Enum | Values |
|---|---|
| `ProcurementBatchStatus` | `Built`, `Manifested`, `Purchasing`, `HandedOff`, `Completed`, `Cancelled` |
| `MarketSessionStatus` | `Draft`, `Open`, `Closed` |
| `MarketSessionCreatedSource` | `Auto`, `Manual` |
| `ProcurementExceptionType` | `Unavailable`, `Shortfall`, `PriceDiscrepancy`, `Damaged` |

> **`ProcurementBatch` is the phiên chợ.** There is no separate `Session` aggregate for the
> shopping run; `MarketSession` is the *scheduling* entity (which market, which day, which
> agents and vehicles) that batches are attached to.

---

## 5. Hub

Source: constants on the Hub domain entities. **SCREAMING_CASE**, except `CrossDockTransfer`.

| Entity | Field | Values |
|---|---|---|
| `HubInboundEvent` | status | `PENDING`, `ARRIVED_AT_HUB` |
| `HubInboundEvent` | condition | `OK` |
| `HubDiscrepancy` | condition | `MISSING`, `DAMAGED`, `PARTIAL` |
| `HubDiscrepancy` | status | `OPEN`, `ACKNOWLEDGED` |
| `HubSortingProgress` | status | `PENDING`, `SORTED` |
| `HubHandoverEvent` | status | `PENDING_CHECKOUT`, `CHECKED_OUT` |
| `CrossDockTransfer` | status | `pending`, `in_progress`, `completed` — **lowercase** |

---

## 6. Logistics

Source: `FreshFlow.Logistics.Domain`. **lowercase / snake_case** throughout.

| Enum or entity | Values |
|---|---|
| `Delivery` status | `pending`, `arrived`, `delivered`, `failed` |
| `DeliveryIssue` type | `undeliverable`, `damaged`, `customer_rejected`, `other` |
| `DeliveryIssue` status | `open`, `resolved` |
| `RouteStatus` | `planned`, `selected`, `reviewed`, `assigned`, `in_progress`, `completed`, `cancelled` |
| `RouteType` | `direct`, `hub_relay` |
| `RoutePlanStatus` | `proposed`, `approved`, `stale`, `superseded` |
| `StopEntityType` | `market`, `restaurant`, `hub` |
| `OptimizationCriteria` | `distance`, `time`, `cost` |
| `VehicleType` | `van`, `truck`, `motorbike` |

---

## 7. Invoicing

| Enum | Values |
|---|---|
| `InvoiceStatus` | `Draft`, `PendingIssuance`, `Issued`, `Failed`, `Adjusted`, `Cancelled` |
| VAT special codes | `KCT` (không chịu thuế), `KKKNT` (không kê khai nộp thuế) |

---

## 8. Notifications

Source: `FreshFlow.Notifications.Domain.Enums`. **snake_case.**

| Enum | Values |
|---|---|
| `NotificationType` | `order_status`, `delivery_update`, `credit_alert`, `credit_statement`, `system` |
| `NotificationSendStatus` | `pending`, `sent`, `failed` |
| `NotificationDevicePlatform` | `ios`, `android`, `web` |

---

## 9. Error codes

The complete allow-list — and the HTTP status each code maps to — lives in
`src/FreshFlow.API/Extensions/ErrorExtensions.cs`. A code that is **not** in that file returns
**500**. The mapping rules are summarized in [`04-api-design.md`](./04-api-design.md) §4.

Common Auth/Admin codes:

| Value | Common case | HTTP |
|---|---|---|
| `VALIDATION_ERROR` | Invalid request body or query parameters | 400 |
| `UNAUTHORIZED` | Missing/invalid access token | 401 |
| `INVALID_CREDENTIALS` | Login identifier/password incorrect | 401 |
| `INVALID_CURRENT_PASSWORD` | Change-password current password incorrect | 401 |
| `REFRESH_TOKEN_EXPIRED` | Refresh token expired | 401 |
| `REFRESH_TOKEN_REVOKED` | Refresh token missing or revoked | 401 |
| `TOKEN_EXPIRED` | Access token expired | 401 |
| `FORBIDDEN` | Valid token, insufficient role | 403 |
| `MARKET_ACCESS_DENIED` | Market agent touching an unassigned market | 403 |
| `HUB_ACCESS_DENIED` | Hub staff touching an unassigned hub | 403 |
| `*_NOT_FOUND` | Resource absent or soft-deleted | 404 |
| `EMAIL_ALREADY_EXISTS` / `PHONE_ALREADY_EXISTS` | Duplicate on user creation | 409 |
| `REFRESH_TOKEN_REUSE` | Rotated refresh token reused — family invalidated | 409 |
| `ALREADY_APPROVED` | Restaurant already approved | 409 |
| `ACCOUNT_INACTIVE` | Account deactivated | 422 |
| `CANNOT_DEACTIVATE_SELF` | Admin deactivating their own account | 422 |
| `CHANNEL_NOT_SUPPORTED` | `PHONE` channel requested (not implemented in v1) | 422 |
| `INVALID_MARKET` | Market invalid for market-agent creation | 422 |
| `ACCOUNT_LOCKED` | Too many failed logins | 423 |
| `ROLE_NOT_CONFIGURED` / `INTERNAL_ERROR` | Server-side misconfiguration or unhandled error | 500 |

---

## 10. Removed

`UserRole` (C# enum) was replaced by the `roles` lookup table. The file still exists but is
empty — use the role strings in §1.
