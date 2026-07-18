# Frontend Enum Values

Enum-like API values the frontend can safely centralize.

**Reconciled with code:** 2026-07-18.

> ⚠️ **Casing is not uniform across modules.** No `JsonStringEnumConverter` is registered, so
> .NET's default enum serialization is never used — every DTO declares its status field as
> `string` and the Application layer converts explicitly. The conversion differs per module:
>
> | Module | Conversion | Resulting casing |
> |---|---|---|
> | Orders | explicit `switch` (`OrderDtoMapper.ToApiStatus`) | `snake_case` |
> | Procurement | `.ToString()` on a PascalCase enum | `PascalCase` |
> | Logistics, Notifications | `.ToString()` on an already-lowercase enum | `snake_case` |
> | Hub | plain `const string` fields, no enum | `SCREAMING_SNAKE_CASE` |
> | Delivery (`deliveries.status`) | plain `const string` fields | `lowercase` |
>
> The DB column and the API value are **not always the same string**: `orders."Status"` stores
> `PickedUp` but the API returns `picked_up`.

---

## RoleName

Source: `FreshFlow.Auth.Domain.Enums.RoleNames` + the `roles` table.
These six are the only roles that exist — `[Authorize(Roles = …)]` with any other name fails
silently (permanent 403).

| Value | Meaning |
|---|---|
| `admin` | System administrator |
| `market_agent` | Market agent |
| `restaurant` | Restaurant user |
| `hub_staff` | Hub staff |
| `driver` | Driver |
| `operations_manager` | Operations manager |

```ts
export const RoleName = {
  Admin: 'admin',
  MarketAgent: 'market_agent',
  Restaurant: 'restaurant',
  HubStaff: 'hub_staff',
  Driver: 'driver',
  OperationsManager: 'operations_manager',
} as const;

export type RoleName = (typeof RoleName)[keyof typeof RoleName];
```

## AdminCreateUserRole

`POST /api/v1/admin/users` accepts these role values.

| Request value | Notes |
|---|---|
| `market_agent` | Creates a Market Agent user |
| `kiosk_staff` | Legacy alias; backend stores and returns `market_agent` |
| `hub_staff` | Creates a Hub Staff user |
| `driver` | Creates a Driver user |
| `restaurant` | Creates a Restaurant user |

## RestaurantStatus

Source: `FreshFlow.Auth.Domain.Enums.RestaurantStatus`.
Stored in `restaurants.status` **lowercase**.

| API value | Meaning |
|---|---|
| `pending` | Awaiting admin approval |
| `active` | Approved and able to order |
| `suspended` | Suspended by admin |

---

## OrderStatus

Source: `FreshFlow.Orders.Domain.Enums.OrderStatus` → `OrderDtoMapper.ToApiStatus`.
**DB stores PascalCase, the API returns snake_case** — do not compare API values against the
column directly.

| API value | DB value | Meaning |
|---|---|---|
| `draft` | `Draft` | Cart being built; items still editable |
| `confirmed` | `Confirmed` | Submitted; charged against the credit limit |
| `batched` | `Batched` | Attached to a procurement batch |
| `picked_up` | `PickedUp` | Purchased by the market agent |
| `at_hub` | `AtHub` | Received at the hub |
| `delivering` | `Delivering` | Out for delivery |
| `delivered` | `Delivered` | Delivered to the restaurant |
| `cancelled` | `Cancelled` | Cancelled (by restaurant, admin, or a cancelled session) |

```ts
export const OrderStatus = {
  Draft: 'draft',
  Confirmed: 'confirmed',
  Batched: 'batched',
  PickedUp: 'picked_up',
  AtHub: 'at_hub',
  Delivering: 'delivering',
  Delivered: 'delivered',
  Cancelled: 'cancelled',
} as const;

export type OrderStatus = (typeof OrderStatus)[keyof typeof OrderStatus];
```

## OrderPaymentStatus

B2B credit (công nợ) model — there is no payment gateway.
Source: `OrderDtoMapper.ToApiPaymentStatus`.

| API value | Meaning |
|---|---|
| `not_applicable` | Not yet confirmed; no debt accrued |
| `outstanding` | Confirmed; amount owed against the credit limit |
| `settled` | Debt fully settled |
| `waived` | Cancelled before settlement; no debt remains |

## OrderIssueType / OrderIssueStatus

`POST /api/v1/orders/{orderId}/issues` — PascalCase.

| `OrderIssueType` | `OrderIssueStatus` |
|---|---|
| `Missing`, `Wrong`, `Damaged` | `Open`, `Resolved` |

## RecurrenceType

Scheduled orders (`/orders/scheduled`): `Daily`, `Weekly`.

## CreditTransactionType

`GET /api/v1/restaurants/{restaurantId}/credit/transactions`.

| Value | Numeric | Meaning |
|---|---|---|
| `Charge` | 1 | Order confirmed — debt increases |
| `Settlement` | 2 | Restaurant paid |
| `Refund` | 3 | Refund credited back |
| `Adjustment` | 4 | Manual admin correction (also used for hub refunds) |

## CreditAlertLevel

| Value | Numeric | Meaning |
|---|---|---|
| `None` | 0 | Under the warning threshold |
| `Warning` | 1 | Approaching the credit limit |
| `Exceeded` | 2 | Over the credit limit |

## PaymentMethod

Settlement recording only: `BankTransfer` (1), `Manual` (2).

---

## ProcurementBatchStatus

Source: `FreshFlow.Procurement.Domain.Enums.ProcurementBatchStatus`, surfaced with
`.ToString()` — **PascalCase in the API**.

| Value | Meaning |
|---|---|
| `Built` | Session created from confirmed orders |
| `Manifested` | Shopping manifest generated |
| `Purchasing` | Agent assigned and buying |
| `HandedOff` | Goods handed over to the hub |
| `Cancelled` | Session cancelled; every covered order cancelled with it |

`Cancelled` is terminal and is excluded from active/pending rollups in Analytics and in
`GET /admin/order-groups/progress`.

## ProcurementExceptionType

`POST /api/v1/procurement/tasks/{batchId}/exceptions` — PascalCase.

`Unavailable`, `Shortfall`, `PriceDiscrepancy`, `Damaged`

---

## Hub statuses — `SCREAMING_SNAKE_CASE`

Plain `const string` fields on the entities, not enums.

| Source | Values |
|---|---|
| `HubInboundEvent.Status` | `PENDING`, `ARRIVED_AT_HUB` |
| `HubInboundEvent.Condition` | `OK` |
| `HubDiscrepancy.Condition` | `MISSING`, `DAMAGED`, `PARTIAL` |
| `HubDiscrepancy.Status` | `OPEN`, `ACKNOWLEDGED` |
| `HubHandoverEvent.Status` | `PENDING_CHECKOUT`, `CHECKED_OUT` |
| `CrossDockTransfer.Status` | `pending`, `in_progress`, `completed` ⚠️ lowercase, unlike its siblings |

---

## Logistics

All lowercase — the enum members themselves are lowercase, surfaced with `.ToString()`.

| Enum | Values |
|---|---|
| `RouteStatus` | `planned`, `selected`, `reviewed`, `assigned`, `in_progress`, `completed`, `cancelled` |
| `RouteType` | `direct`, `hub_relay` |
| `VehicleType` | `van`, `truck`, `motorbike` |
| `OptimizationCriteria` | `distance`, `time`, `cost` |
| `StopEntityType` | `market`, `restaurant` |

## Delivery

`const string` fields, lowercase.

| Source | Values |
|---|---|
| `Delivery.Status` | `pending`, `arrived`, `delivered`, `failed` |
| `DeliveryIssue.Type` | `undeliverable`, `damaged`, `customer_rejected`, `other` |
| `DeliveryIssue.Status` | `open`, `resolved` |

---

## Notifications

| Enum | Values |
|---|---|
| `NotificationType` | `order_status`, `delivery_update`, `credit_alert`, `system` |
| `NotificationSendStatus` | `pending`, `sent`, `failed` |
| `NotificationDevicePlatform` | `ios`, `android`, `web` |

---

## Error codes → HTTP status

`ErrorExtensions.ToActionResult()` (`src/FreshFlow.API/Extensions/ErrorExtensions.cs`) maps
error codes to HTTP status. Only `_NOT_FOUND` is matched by pattern (suffix); **every other
code is an explicit allow-list entry**, so a code that is not listed there falls through to
**HTTP 500**. Always reuse an existing code.

| HTTP | Codes |
|---|---|
| 404 | anything ending in `_NOT_FOUND` |
| 400 | `VALIDATION_ERROR`, `INVALID_ROLE`, `WEAK_PASSWORD`, `RESET_TOKEN_INVALID`, `OTP_INVALID`, and the business-rule list (`CREDIT_LIMIT_EXCEEDED`, `ORDER_EMPTY`, `INSUFFICIENT_STOCK`, …) |
| 401 | `UNAUTHORIZED`, `INVALID_CREDENTIALS`, `INVALID_CURRENT_PASSWORD`, `TOKEN_INVALID`, `REFRESH_TOKEN_EXPIRED`, `REFRESH_TOKEN_REVOKED` |
| 403 | `FORBIDDEN`, `MARKET_ACCESS_DENIED` |
| 409 | duplicate/state-conflict list — `EMAIL_ALREADY_EXISTS`, `ALREADY_APPROVED`, `PLATE_NUMBER_DUPLICATE`, `BATCH_NOT_CANCELLABLE`, `OPTIMISTIC_CONCURRENCY_CONFLICT`, … |
| 423 | `ACCOUNT_LOCKED` |

### Auth / Admin error codes

| Value | Common case |
|---|---|
| `UNAUTHORIZED` | Missing/invalid access token, or user no longer active |
| `FORBIDDEN` | Valid token but insufficient role |
| `VALIDATION_ERROR` | Invalid request body or query parameters |
| `INVALID_CREDENTIALS` | Login identifier/password is incorrect |
| `INVALID_CURRENT_PASSWORD` | Change password current password is incorrect |
| `ACCOUNT_INACTIVE` | Account is deactivated |
| `ACCOUNT_LOCKED` | Too many failed logins |
| `TOKEN_EXPIRED` | Access token has expired |
| `REFRESH_TOKEN_EXPIRED` | Refresh token has expired |
| `REFRESH_TOKEN_REVOKED` | Refresh token is missing or revoked |
| `REFRESH_TOKEN_REUSE` | Rotated refresh token was reused |
| `EMAIL_ALREADY_EXISTS` | Admin create user duplicate email |
| `PHONE_ALREADY_EXISTS` | Admin create user duplicate phone |
| `ALREADY_APPROVED` | Restaurant is already approved |
| `INVALID_MARKET` | Market is invalid for Market Agent creation |
| `CANNOT_DEACTIVATE_SELF` | Admin attempted to deactivate their own account |
| `USER_NOT_FOUND` | User was not found |
| `RESTAURANT_NOT_FOUND` | Restaurant was not found |
| `INTERNAL_ERROR` | Unexpected server error |
