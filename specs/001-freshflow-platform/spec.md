# Feature Specification: FreshFlow Platform

**Feature Branch**: `001-freshflow-platform`
**Created**: 2026-05-11
**Status**: Draft
**Input**: FreshFlow wholesale market food procurement and logistics platform

---

## User Scenarios & Testing

### User Story 1 - Real-Time Price Updates (Priority: P1)

A Kiosk Staff member arrives at a wholesale market between 2–4 AM. As products
arrive at their stall, they open the mobile app, select a product, enter the new
price and available quantity, and confirm. Within 2 seconds, every connected
Restaurant user who has that market's price board open sees the updated price
without refreshing the page.

**Why this priority**: This is the core value proposition. Without live prices,
restaurants have no reason to use the platform instead of going to the market
themselves.

**Independent Test**: A Kiosk Staff member updates a product price while a
Restaurant user has the market board open. The Restaurant user sees the new price
within 2 seconds. Delivers the live price board MVP with no other features needed.

**Acceptance Scenarios**:

1. **Given** a Kiosk Staff member is assigned to Market A, **When** they submit a
   price update for a product at Market A, **Then** all Restaurant users currently
   viewing Market A's price board see the new price within 2 seconds — no manual
   refresh required.
2. **Given** a Kiosk Staff member assigned to Market A, **When** they attempt to
   update a price at Market B, **Then** the system rejects the action with a
   clear permission error.
3. **Given** no Restaurant users are currently connected to the market board,
   **When** a price is updated, **Then** the update is stored successfully and
   any user who connects afterward sees the current price immediately.
4. **Given** a price or quantity update is submitted, **When** it is processed,
   **Then** the previous value is preserved in history — the record is never
   overwritten.

---

### User Story 2 - Order Placement (Priority: P2)

A Restaurant purchasing manager opens the app between 5–7 AM, browses the live
price board across one or more markets, selects products and quantities, and
places an order. The process takes 3 steps or fewer and requires no training or
manual.

**Why this priority**: Order placement is the revenue-generating action. Price
visibility alone is not enough — restaurants must be able to act on the prices
they see.

**Independent Test**: A Restaurant user views prices and places a single order.
The order is created and a confirmation is shown. Delivers an end-to-end buy
flow without logistics scheduling.

**Acceptance Scenarios**:

1. **Given** a Restaurant user is viewing market prices, **When** they select
   products, specify quantities, and confirm, **Then** the order is created with
   status PENDING and an order ID is returned.
2. **Given** a product has insufficient available quantity for the requested
   amount, **When** the restaurant submits the order, **Then** the system rejects
   that line item and clearly shows the product name, requested quantity, and
   current available quantity.
3. **Given** two restaurants simultaneously request the last available unit of a
   product, **Then** exactly one order succeeds; the other receives an
   INSUFFICIENT_STOCK error.
4. **Given** a Restaurant user has placed a PENDING order, **When** they cancel
   it, **Then** the order transitions to CANCELLED and the reserved stock is
   released back to available inventory.
5. **Given** a Restaurant user attempts to create an order for a different
   restaurant's account, **Then** the system rejects the action.

---

### User Story 3 - Scheduled Recurring Orders (Priority: P3)

A Restaurant purchasing manager sets up a recurring order template (for example,
the same product list every Monday, Wednesday, and Friday). Once configured, the
system automatically generates and submits a new order at each scheduled time
with no further action from the restaurant.

**Why this priority**: Recurring orders eliminate the daily manual effort for
restaurants with predictable purchasing patterns — one of the key inefficiencies
identified in the problem statement.

**Independent Test**: Configure a daily schedule and verify that a concrete order
is automatically created at the specified time. The restaurant can view the
generated instance in their order history.

**Acceptance Scenarios**:

1. **Given** a Restaurant user creates a schedule with DAILY cadence and a start
   time, **When** the scheduled time arrives, **Then** a concrete order is
   automatically created with status PENDING and the restaurant is notified.
2. **Given** a recurring schedule exists and the system was offline at the
   scheduled time, **When** the system recovers, **Then** the missed order
   instance is created immediately — it is not silently skipped.
3. **Given** a Restaurant user cancels a recurring schedule, **When** the next
   scheduled time passes, **Then** no new order instance is created.
4. **Given** a Restaurant user views their order history, **Then**
   auto-generated order instances are listed and each shows a link to the
   parent schedule.

---

### User Story 4 - Delivery Route Calculation and Scheduling (Priority: P4)

An Admin selects one or more confirmed orders, optionally groups them into a
logistics batch, and asks the system to calculate the best delivery route from
the source market(s) to the destination restaurant(s). The system returns an
ordered stop list with estimated distances, travel time, and cost within 3
seconds. The Admin then creates a delivery schedule.

**Why this priority**: Route calculation is the logistics differentiator that
allows FreshFlow to aggregate orders and share delivery costs — making the
platform financially viable for restaurants.

**Independent Test**: An Admin calculates a route for a single confirmed order
and creates a delivery schedule with an assigned vehicle. The order transitions
to IN_TRANSIT when the departure time is reached.

**Acceptance Scenarios**:

1. **Given** an Admin provides source markets and destination restaurants, **When**
   they request a route with an optimization criterion (distance, time, or cost),
   **Then** the system returns a route within 3 seconds showing stop order,
   estimated distance (km), estimated travel time (minutes), and estimated cost.
2. **Given** a calculated route, **When** the Admin assigns a vehicle and sets a
   departure time, **Then** a delivery schedule is created with status SCHEDULED.
3. **Given** the total weight of the order group exceeds the vehicle's capacity,
   **When** the Admin creates the schedule, **Then** the system rejects it and
   shows actual weight vs. vehicle capacity.
4. **Given** a vehicle is already scheduled for another delivery in the same time
   window, **When** the Admin tries to assign it, **Then** the system rejects the
   assignment and indicates the conflict.
5. **Given** an Admin includes hub stops in the route, **When** the route is
   calculated, **Then** the stop list correctly sequences market → hub → restaurant
   stops and the route is flagged as HUB_RELAY type.

---

### User Story 5 - Real-Time Order Status Tracking (Priority: P5)

A Restaurant user can check the status of any active order at any time. When an
order's status changes (e.g., CONFIRMED → IN_TRANSIT → DELIVERED), the
restaurant user's screen updates automatically within 2 seconds — no manual
refresh needed.

**Why this priority**: Real-time status tracking directly addresses the "where
is my order?" anxiety that currently drives daily phone calls and physical
market visits.

**Independent Test**: Place an order and have an Admin advance its status while a
Restaurant user observes the screen. The Restaurant user's view updates within
2 seconds without refreshing.

**Acceptance Scenarios**:

1. **Given** a Restaurant user has an active order on screen, **When** an Admin
   advances the order status, **Then** the displayed status updates within
   2 seconds — no refresh required.
2. **Given** a Restaurant user is not connected when a status change occurs,
   **When** they reconnect, **Then** they see the current (most recent) status
   immediately.
3. **Given** a Restaurant user views an order, **Then** the order displays its
   full status history in chronological order (each transition with timestamp
   and actor).

---

### User Story 6 - Order Aggregation (Priority: P6)

An Admin groups multiple confirmed restaurant orders that share overlapping
delivery routes into a single logistics batch. This allows one vehicle to serve
multiple restaurants in one trip, sharing the delivery cost.

**Why this priority**: Aggregation is the primary cost-sharing mechanism that
makes FreshFlow's delivery pricing competitive against restaurants hiring
individual vehicles.

**Independent Test**: An Admin groups two confirmed orders from different
restaurants, calculates a shared route, and creates a delivery schedule.

**Acceptance Scenarios**:

1. **Given** multiple confirmed orders exist, **When** an Admin creates an order
   group from them, **Then** all member orders are linked to the group and the
   group appears in the logistics view.
2. **Given** an order is already in a group, **When** an Admin tries to add it to
   a second active group, **Then** the system rejects the action.
3. **Given** an order group has been created, **When** the Admin calculates a
   delivery route, **Then** the route covers all source markets and destination
   restaurants for every order in the group.

---

### User Story 7 - Price Trend Dashboard (Priority: P7)

A Restaurant user views the historical price movement for a product at a specific
market — daily data for up to 12 months. They use this information to decide
whether to buy at today's price or wait.

**Why this priority**: Price history converts FreshFlow from a transactional
tool into a decision-support platform, increasing daily engagement and stickiness.

**Independent Test**: A Restaurant user selects a product and market and views
a price trend chart for the past 30 days.

**Acceptance Scenarios**:

1. **Given** a Restaurant user selects a product and market, **When** they open
   the price trend view, **Then** daily price data for the past 30 days is shown
   by default, with an option to extend the range up to 12 months.
2. **Given** a product has fewer than 7 days of price history, **When** the trend
   is viewed, **Then** all available data points are shown without error or empty
   state.
3. **Given** an Admin views analytics, **Then** they can see order demand volume
   (count and quantity) alongside price data for the same product and market.
4. **Given** an Admin requests a price history export, **When** the export is
   ready, **Then** the file can be downloaded within 5 minutes of the request.

---

### Edge Cases

- What happens when Kiosk Staff loses internet connectivity mid-update? The
  submission fails with a clear offline indicator; no partial data is saved.
- What happens if the scheduled order time passes during a system restart? The
  system creates the missed order instance upon recovery — silent skips are
  forbidden.
- What happens if a restaurant places an order and the kiosk has since set
  quantity to zero? The order is rejected at submission time with INSUFFICIENT_STOCK
  showing the current available quantity (zero).
- What happens during peak load (2–5 AM with hundreds of price updates per
  minute)? Price updates must still reach all Restaurant users within 2 seconds.
- What happens if a vehicle breaks down mid-route? The Admin manually transitions
  the delivery schedule status; automatic vehicle tracking is out of scope.

---

## Requirements

### Functional Requirements

**Authentication & Access Control**

- **FR-001**: The system MUST require every user to authenticate with email and
  password before accessing any feature.
- **FR-002**: The system MUST enforce role-based access: Kiosk Staff can only
  update prices for their assigned market; Restaurants can only view prices and
  manage their own orders; Admins can manage all data and logistics.
- **FR-003**: The system MUST support session renewal so users remain logged in
  for up to 7 days without re-entering their password, with automatic session
  invalidation on logout.
- **FR-004**: Admins MUST be able to create, approve, and deactivate user
  accounts for all roles.

**Real-Time Price Management**

- **FR-005**: Kiosk Staff MUST be able to update a product's price and available
  quantity in a single action from a mobile device.
- **FR-006**: All connected Restaurant users viewing the relevant market board
  MUST see price updates within 2 seconds of the Kiosk Staff's submission.
- **FR-007**: The system MUST store an immutable record of every price and
  quantity change (value, actor, timestamp). Records MUST NOT be modifiable or
  deletable.
- **FR-009**: Admins MUST be able to create, update, and deactivate products
  in the system-wide catalog.

**Order Management**

- **FR-010**: Restaurant users MUST be able to place a bulk order with one or
  more line items (product, market, quantity).
- **FR-011**: The system MUST reject any order line item where the requested
  quantity exceeds available stock, showing available quantity per failing item.
- **FR-012**: On successful order creation, the system MUST soft-reserve the
  requested stock, preventing double-allocation for 30 minutes.
- **FR-013**: Restaurant users MUST be able to cancel orders with status PENDING
  or CONFIRMED; cancellation of IN_TRANSIT or DELIVERED orders MUST be rejected.
- **FR-014**: Restaurant users MUST be able to view their full order history
  paginated, filtered by status, and sorted by date.
- **FR-015**: Restaurant users MUST be able to create recurring orders with DAILY
  or WEEKLY cadence, specifying a start time in the Vietnam timezone.
- **FR-016**: The system MUST automatically generate concrete order instances at
  each scheduled time; missed instances due to downtime MUST be created on
  recovery.

**Logistics**

- **FR-017**: Admins MUST be able to calculate a delivery route from source
  markets (optionally via hub stops) to destination restaurants, choosing
  optimization by distance, time, or cost. Calculation MUST complete within
  3 seconds for up to 20 stops.
- **FR-018**: Admins MUST be able to register and manage vehicles (capacity,
  type, active status).
- **FR-019**: Admins MUST be able to create a delivery schedule linking a route,
  a vehicle, and a departure time; the system MUST validate vehicle capacity
  against total order group weight and detect vehicle double-booking.
- **FR-020**: The system MUST automatically group confirmed, unbatched orders
  into logistics batches at the daily cutoff and allow Admins to trigger the
  same batching logic manually through an idempotent API with dry-run support.

**Hub Management**

- **FR-021**: Admins MUST be able to register distribution hubs with name,
  address, and capacity.
- **FR-022**: The system MUST record inbound and outbound goods events at each
  hub and update the hub's stock balance within the same operation — balance and
  event records MUST never be out of sync.

**Real-Time Notifications**

- **FR-023**: Restaurant users MUST receive order status change notifications
  within 2 seconds of the change, without polling.
- **FR-024**: Restaurant users MUST receive delivery status updates (departed,
  arrived at hub, delivered) within 2 seconds, without polling.

**Analytics**

- **FR-025**: Restaurant users MUST be able to view historical daily price data
  for any product at any market for up to 12 months.
- **FR-026**: Admins MUST be able to view demand heatmaps and delivery
  performance metrics (on-time rate, average duration, vehicle utilisation).
- **FR-027**: Admins MUST be able to request an asynchronous export of price
  history data and download the file when ready.

### Key Entities

- **User**: System account with role (Admin, Kiosk Staff, Restaurant). Kiosk
  Staff are assigned to one or more markets.
- **Market**: A wholesale food market (Hoc Mon, Binh Dien, Thu Duc). Has name,
  location, and active status.
- **Product**: An item in the system-wide catalog (name, unit, category). Shared
  across all markets; prices vary per market.
- **Price Snapshot**: Immutable record of a price or quantity change at a
  specific market for a specific product. Append-only — never modified.
- **Order**: A restaurant's purchase request with status lifecycle:
  PENDING → CONFIRMED → BATCHED → IN_TRANSIT → DELIVERED (or CANCELLED from PENDING/CONFIRMED).
- **Order Line Item**: One row in an order: product, market, quantity, and
  price at time of order.
- **Scheduled Order**: A recurrence definition (DAILY or WEEKLY, start time,
  line items). Generates Order instances automatically.
- **Order Group**: A logistics batch linking multiple confirmed Orders for
  shared delivery.
- **Vehicle**: A delivery vehicle (plate number, capacity in kg, type, status).
- **Delivery Route**: A calculated route with ordered stops, estimated distances,
  travel times, and cost. Calculated on demand; cached for identical requests.
- **Delivery Schedule**: An assignment of route + vehicle + departure time +
  order group. Status: SCHEDULED → IN_PROGRESS → COMPLETED (or CANCELLED).
- **Hub**: A distribution centre (name, location, capacity in kg).
- **Hub Stock**: Running inventory balance per product per hub, updated on
  every inbound and outbound event.

---

## Success Criteria

### Measurable Outcomes

- **SC-001**: A Kiosk Staff price submission is visible to all connected
  Restaurant users within 2 seconds, measured end-to-end under normal peak
  load (2–9 AM window).
- **SC-002**: A Restaurant user can complete an order (from browsing prices to
  order confirmation) in 3 or fewer distinct interactions.
- **SC-003**: Delivery route calculation for up to 20 stops returns a result
  in under 3 seconds in all cases.
- **SC-004**: The system handles at least 500 concurrent active sessions during
  peak hours without any feature becoming unavailable or degraded.
- **SC-005**: A scheduled recurring order instance is created within 60 seconds
  of the scheduled time, including after a system restart.
- **SC-006**: An Admin can see the current status of all active orders, vehicles,
  and live prices from a single screen without navigating away.
- **SC-007**: Zero price update submissions are silently lost — every submission
  either succeeds and is visible to Restaurant users, or fails with a clear error
  shown to the Kiosk Staff member.
- **SC-008**: An Admin price history export file is available for download within
  5 minutes of the request being submitted.

---

## Assumptions

- All three user types access the platform via internet-connected devices.
  Offline operation beyond a clear error message is out of scope.
- The product catalog is managed exclusively by Admins; Kiosk Staff cannot add
  or remove products.
- All schedules and timestamps operate in the Vietnam timezone (UTC+7).
- Restaurant self-registration is not available; restaurant accounts are created
  and approved by Admin.
- AI-based price recommendations ("buy now or wait") are out of scope for this
  version.
- Payment processing is out of scope; FreshFlow coordinates procurement and
  logistics but does not handle financial transactions.
- Real-time GPS vehicle tracking is out of scope; delivery status is represented
  by manual status transitions (SCHEDULED → IN_TRANSIT → DELIVERED).
- The platform is designed primarily for the 2–9 AM peak window; off-peak
  performance degradation is acceptable provided the system recovers automatically.
- This is a university capstone project. Polished UI is secondary to working
  end-to-end functionality.
