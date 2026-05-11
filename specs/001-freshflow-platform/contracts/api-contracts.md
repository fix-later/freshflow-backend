# API Contracts: FreshFlow Platform

**Branch**: `001-freshflow-platform` | **Date**: 2026-05-11
**Source**: `docs/04-api-design.md` (authoritative)

> This file is an implementation-facing summary. For full field-level documentation,
> validation rules, and response envelope details, consult `docs/04-api-design.md`.

---

## Base URL and Conventions

```
https://api.freshflow.vn/api/v1
```

- All responses wrapped in `{ "success": bool, "data": ..., "meta": ... }` envelope
- Errors: `{ "success": false, "error": { "code": "...", "message": "...", "details": [...] } }`
- Field naming: **camelCase** in API, snake_case in DB
- Datetimes: ISO 8601 with `+07:00` offset
- Soft-deleted resources return 404
- Rate limit exceeded: 429 with `Retry-After` header

### Pagination
- **Cursor-based**: price history, notifications (high-volume append-heavy)
- **Offset-based**: orders, admin lists, analytics exports

### Authentication
- REST: `Authorization: Bearer <accessToken>` header
- SignalR negotiate: `?access_token=<accessToken>` query param

---

## Auth Endpoints

### POST /api/v1/auth/login
**Role**: Public

**Request**: `{ email, password }`

**Response 200**:
```json
{
  "success": true,
  "data": {
    "accessToken": "<jwt>",
    "refreshToken": "<opaque-token>",
    "expiresIn": 900,
    "user": { "id": "<uuid>", "email": "...", "role": "restaurant" }
  }
}
```

**Errors**: 401 `INVALID_CREDENTIALS` (wrong password OR email not found — same code, no enum)

---

### POST /api/v1/auth/refresh
**Role**: Public

**Request**: `{ refreshToken: "<token>" }`

**Response 200**: Same shape as login (new accessToken + new refreshToken)

**Errors**: 401 `REFRESH_TOKEN_EXPIRED`, 401 `REFRESH_TOKEN_REUSE` (family invalidated)

---

### POST /api/v1/auth/logout
**Role**: All authenticated

**Request**: `{ refreshToken: "<token>" }` + Bearer header

**Response**: 204 No Content

---

### POST /api/v1/auth/register
**Role**: Admin only

**Request**: `{ email, password, role, name }` (role = `kiosk_staff` | `restaurant`)

**Response 201**: `{ "data": { "userId": "<uuid>" } }`

**Errors**: 409 `EMAIL_ALREADY_EXISTS`

---

## Pricing Endpoints

### GET /api/v1/markets
**Role**: All authenticated | **Response**: Array of market objects with id, name, latitude, longitude

### GET /api/v1/products
**Role**: All authenticated | **Query**: `?includeInactive=true` (Admin only)

### POST /api/v1/admin/products
**Role**: Admin only | **Body**: `{ name, unit, category, description? }`
**Response 201**: `{ "data": { "productId": "<uuid>" } }`

### GET /api/v1/markets/{marketId}/products
**Role**: All authenticated
**Response**: Array of `{ productId, name, unit, category, currentPrice, currentQuantity, availableQuantity, updatedAt }`

### PATCH /api/v1/markets/{marketId}/products/{productId}/price
**Role**: Kiosk Staff (assigned to this market only)
**Body**: `{ price: decimal }` — must be > 0
**Response 200**: `{ updatedPrice, updatedAt }`
**Errors**: 403 if market assignment mismatch, 422 if price ≤ 0

### PATCH /api/v1/markets/{marketId}/products/{productId}/quantity
**Role**: Kiosk Staff (assigned to this market only)
**Body**: `{ quantity: integer }` — must be ≥ 0
**Response 200**: `{ updatedQuantity, updatedAt }`

### GET /api/v1/markets/{marketId}/products/{productId}/price-history
**Role**: Admin, Restaurant
**Query**: `?cursor=<base64>&pageSize=20`
**Response (cursor-paginated)**: `{ data: [{ price, quantity, recordedBy, recordedAt }], meta: { nextCursor } }`

### PATCH /api/v1/admin/config/significant-price-threshold
**Role**: Admin only | **Body**: `{ thresholdPercent: decimal }` — range 1–50

---

## Order Endpoints

### POST /api/v1/orders
**Role**: Restaurant only
**Body**:
```json
{
  "items": [
    { "marketProductId": "<uuid>", "quantity": 5 }
  ],
  "notes": "optional"
}
```
**Response 201**: `{ "data": { "orderId": "<uuid>", "status": "pending", "totalAmount": 0 } }`
**Errors**: 422 `INSUFFICIENT_STOCK` (per-item detail), 422 `INVALID_PRODUCT`

### GET /api/v1/orders
**Role**: Restaurant (own orders), Admin (all)
**Query**: `?status=&page=&pageSize=&sort=createdAt:desc`
**Response (offset-paginated)**

### GET /api/v1/orders/{orderId}
**Role**: Restaurant (own only), Admin
**Errors**: 403 if wrong restaurant

### PATCH /api/v1/orders/{orderId}/cancel
**Role**: Restaurant (own), Admin
**Response 200** | **Errors**: 409 `ORDER_NOT_CANCELLABLE` (if in_transit/delivered)

### POST /api/v1/orders/scheduled
**Role**: Restaurant only
**Body**: `{ recurrenceType: "daily"|"weekly", dayOfWeek?: 0-6, runTime: "HH:mm", firstRunAt: "<iso8601>", items: [...] }`
**Response 201**: `{ scheduledOrderId, nextRunAt }`

### GET /api/v1/orders/scheduled/{id}/instances
**Role**: Restaurant (own), Admin

### POST /api/v1/admin/order-groups
**Role**: Admin only
**Body**: `{ orderIds: ["<uuid>", ...], name?: "string" }`
**Response 201**: `{ orderGroupId }` | **Errors**: 422 (non-confirmed order), 409 (order already in group)

### PATCH /api/v1/admin/orders/{orderId}/status
**Role**: Admin only
**Body**: `{ status: "confirmed" | "processing" | "ready_for_pickup" | "in_transit" | "delivered" }`
**Errors**: 422 invalid transition

---

## Logistics Endpoints

### POST /api/v1/logistics/routes/calculate
**Role**: Admin only
**Body**: `{ sourceMarketIds: [...], hubIds: [...], destinationRestaurantIds: [...], optimizationCriteria: "distance"|"time"|"cost" }`
**Response 200** (within 3 s): `{ routeId, routeType, stops: [...], totalDistanceKm, totalDurationMin, estimatedCostVnd }`
**Errors**: 422 `STOP_LIMIT_EXCEEDED` (> 20 stops)

### POST /api/v1/logistics/routes/{routeId}/assign-vehicle
**Role**: Admin only | **Body**: `{ vehicleId }` | **Errors**: 409 `VEHICLE_NOT_AVAILABLE`, 422 `VEHICLE_INACTIVE`

### POST /api/v1/logistics/schedules
**Role**: Admin only | **Body**: `{ routeId, vehicleId, orderGroupId, plannedDepartureAt }`
**Response 201**: `{ scheduleId, capacityUtilizationPercent }`
**Errors**: 422 `VEHICLE_CAPACITY_EXCEEDED`

### GET /api/v1/logistics/schedules
**Role**: Admin only | **Query**: `?date=YYYY-MM-DD`

### PATCH /api/v1/admin/deliveries/{deliveryId}/status
**Role**: Admin only | **Body**: `{ status: "picked_up"|"in_transit"|"delivered"|"failed" }`

### POST /api/v1/admin/vehicles
**Role**: Admin only | **Body**: `{ plateNumber, capacityKg, vehicleType }`
**Response 201**: `{ vehicleId }` | **Errors**: 409 duplicate plate

### GET /api/v1/admin/vehicles
**Role**: Admin only | **Query**: `?available=true`

### PATCH /api/v1/admin/vehicles/{id}
**Role**: Admin only

---

## Hub Endpoints

### POST /api/v1/admin/hubs
**Role**: Admin only | **Body**: `{ name, address, latitude, longitude, capacityKg }`
**Response 201**: `{ hubId }`

### GET /api/v1/admin/hubs
**Role**: Admin only

### PATCH /api/v1/admin/hubs/{id}
**Role**: Admin only | **Errors**: 409 `HUB_HAS_PENDING_DELIVERIES` (on deactivation)

### POST /api/v1/hubs/{hubId}/inbound
**Role**: Admin only | **Body**: `{ sourceMarketId, deliveryRouteId, items: [{marketProductId, quantityKg}], arrivedAt }`
**Errors**: 409 `ALREADY_RECEIVED`, 422 `HUB_CAPACITY_EXCEEDED`

### POST /api/v1/hubs/{hubId}/outbound
**Role**: Admin only | **Body**: `{ destinationRouteId, items: [{marketProductId, quantityKg}], dispatchedAt }`
**Errors**: 422 `INSUFFICIENT_HUB_STOCK`

### GET /api/v1/hubs/{hubId}/inventory
**Role**: Admin only

### GET /api/v1/hubs/{hubId}/redistribution-suggestions
**Role**: Admin only | **SLA**: < 2 seconds

---

## Analytics Endpoints

### GET /api/v1/analytics/price-trends
**Role**: Admin, Restaurant | **Query**: `?productId=&marketId=&from=&to=`

### GET /api/v1/analytics/demand-heatmap
**Role**: Admin only | **Query**: `?from=&to=`

### GET /api/v1/analytics/demand-heatmap/time-distribution
**Role**: Admin only | **Returns**: 7×24 matrix

### GET /api/v1/analytics/delivery-performance
**Role**: Admin only | **Query**: `?from=&to=`

### GET /api/v1/analytics/delivery-performance/by-route
**Role**: Admin only

### POST /api/v1/analytics/export
**Role**: Admin only | **Body**: `{ exportType: "price_history", parameters: {...} }`
**Response 202**: `{ jobId }`

### GET /api/v1/analytics/export/{jobId}/status
**Role**: Admin only | **Returns**: `{ status: "pending"|"processing"|"ready"|"failed" }`

### GET /api/v1/analytics/export/{jobId}/download
**Role**: Admin only | **Returns**: CSV file | **Errors**: 404 after 24-hour expiry

---

## Notification Endpoints

### GET /api/v1/notifications
**Role**: All authenticated | **Pagination**: cursor-based | **Sort**: newest first

### PATCH /api/v1/notifications/{id}/read
**Role**: Owner only

### PATCH /api/v1/notifications/read-all
**Role**: All authenticated

---

## SignalR Hubs

### /hubs/pricing (PricingHub)
**Auth**: JWT via `?access_token=` query param on negotiate

| Direction | Method | Group | Payload |
|-----------|--------|-------|---------|
| Client → Server | `JoinMarketGroup(marketId)` | Joins `market:{marketId}` | |
| Client → Server | `LeaveMarketGroup(marketId)` | Leaves group | |
| Server → Client | `PriceUpdated` | `market:{marketId}` | `{ productId, marketId, newPrice, newQuantity, updatedAt }` |
| Server → Client | `SignificantPriceAlert` | `market:{marketId}` | `{ productId, marketId, changePercent, previousPrice, newPrice, severity }` |

Kiosk Staff join `kiosk:{marketId}` automatically on connect (market validated against JWT claims).

### /hubs/orders (OrderHub)
**Auth**: JWT via `?access_token=`

Group join is **automatic** based on JWT claims — no client-invoked method.
- Restaurant role → joins `restaurant:{restaurantId}` on connect
- Admin role → joins `admin:all` on connect

| Direction | Method | Group | Payload |
|-----------|--------|-------|---------|
| Server → Client | `OrderStatusChanged` | `restaurant:{restaurantId}` | `{ orderId, previousStatus, newStatus, changedAt, changedByUserId }` |
| Server → Client | `OrderGrouped` | `restaurant:{restaurantId}` | `{ orderGroupId, orderId, changedAt }` |

### /hubs/delivery (DeliveryHub)
**Auth**: JWT via `?access_token=`

Group join automatic based on JWT claims. Restaurant → `restaurant:{restaurantId}`.

| Direction | Method | Group | Payload |
|-----------|--------|-------|---------|
| Server → Client | `DeliveryStarted` | `restaurant:{restaurantId}` | `{ scheduleId, routeId, estimatedArrivalAt, orderIds[] }` |
| Server → Client | `DeliveryCompleted` | `restaurant:{restaurantId}` | `{ scheduleId, routeId, actualDeliveredAt, orderIds[] }` |
| Server → Client | `DeliveryStatusChanged` | `restaurant:{restaurantId}` | `{ scheduleId, newStatus, updatedAt }` |

**Per-restaurant fan-out**: in a multi-drop delivery, each restaurant only receives events for
its own orders even though the delivery covers multiple restaurants.

---

## Health Check

### GET /health
**Auth**: None | **Excluded from rate limiting**

```json
{
  "status": "Healthy",
  "components": {
    "postgresql": { "status": "Healthy", "latencyMs": 2 },
    "redis": { "status": "Healthy", "latencyMs": 1 }
  },
  "timestamp": "2026-05-11T03:00:00+07:00"
}
```

- PostgreSQL unhealthy → HTTP 503
- Redis unhealthy → HTTP 200 Degraded (graceful degradation)
