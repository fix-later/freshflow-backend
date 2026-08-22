# FreshFlow — Conceptual Entities Description

> Source: [`03A-conceptual-erd-chen-core.drawio`](03A-conceptual-erd-chen-core.drawio) (Chen notation, core conceptual ERD).
> This document describes the **26 core conceptual entities** and the **40 relationships** drawn in that diagram.
> The conceptual model is intentionally reduced: link tables, auth tokens, notifications, audit logs, caches
> and implementation-only event/detail tables are excluded. For the full physical picture (59 entities of the
> EF Core model) see [`03D-entities-description.md`](03D-entities-description.md).
>
> Conceptual names differ from the physical table names on purpose — the mapping column below is the bridge.

---

## 1. Entities

### 1.1 Identity & Customer

| # | Entity | Maps to (physical) | Description |
|---:|---|---|---|
| 1 | User | `users` | An account used to access FreshFlow. |
| 2 | Restaurant | `restaurants` | A customer that places orders. |
| 3 | Driver | `driver_profiles` | A user's delivery profile. |
| 4 | Delivery Address | `delivery_addresses` | A restaurant's receiving location. |

### 1.2 Catalog & Pricing

| # | Entity | Maps to (physical) | Description |
|---:|---|---|---|
| 5 | Product | `products` | A market-independent catalog item. |
| 6 | Product Category | `product_categories` | A hierarchical product group. |
| 7 | Market | `markets` | A wholesale sourcing location. |
| 8 | Market Offering | `market_products` | A product sold at a market with its price and availability. |
| 9 | Price History | `price_snapshots` | Historical price and quantity updates for an offering. |

### 1.3 Ordering

| # | Entity | Maps to (physical) | Description |
|---:|---|---|---|
| 10 | Order | `orders` | A restaurant's purchase request. |
| 11 | Order Item | `order_items` | A product line in an order. |
| 12 | Scheduled Order | `scheduled_orders` | A recurring template that generates orders. |
| 13 | Order Issue | `order_issues` | A problem reported for an order or item. |

### 1.4 Credit (công nợ)

| # | Entity | Maps to (physical) | Description |
|---:|---|---|---|
| 14 | Credit Account | `restaurant_credits` | A restaurant's credit limit and outstanding balance. |
| 15 | Credit Transaction | `credit_transactions` | A charge, settlement, refund or adjustment. |
| 16 | Credit Statement | `credit_statements` | A monthly summary of credit activity. |

### 1.5 Market Session & Procurement

| # | Entity | Maps to (physical) | Description |
|---:|---|---|---|
| 17 | Market Session | `market_sessions` | A buying session for one market and service date. |
| 18 | Procurement Batch | `procurement_batches` | Confirmed orders grouped for purchasing. |
| 19 | Procurement Item | `procurement_batch_items` | An aggregated offering quantity in a batch. |

### 1.6 Hub & Delivery

| # | Entity | Maps to (physical) | Description |
|---:|---|---|---|
| 20 | Hub | `hubs` | A facility that receives, sorts and dispatches goods. |
| 21 | Hub Inventory | `hub_inventories` | The quantity of an offering held at a hub. |
| 22 | Vehicle | `vehicles` | A vehicle used for procurement or delivery. |
| 23 | Delivery Route | `delivery_routes` | A delivery route assigned to a driver and vehicle. |
| 24 | Delivery | `deliveries` | An order fulfilled as a route stop. |

### 1.7 Invoicing

| # | Entity | Maps to (physical) | Description |
|---:|---|---|---|
| 25 | Invoice | `invoices` | A VAT invoice for a delivered order. |
| 26 | Invoice Line | `invoice_lines` | A product or fee line on an invoice. |

---

## 2. Relationships

Cardinalities are read left → right as drawn in the Chen diagram (verified against the EF Core
entity configurations, not the design docs).

| # | Entity A | Card. | Relationship | Card. | Entity B | Meaning |
|---:|---|:--:|---|:--:|---|---|
| 1 | User | 1 | is profile | 0..1 | Restaurant | A user account may be the login of a restaurant. |
| 2 | User | 1 | is profile | 0..1 | Driver | A user account may carry a driver profile. |
| 3 | Restaurant | 1 | ships to | N | Delivery Address | A restaurant keeps several receiving addresses. |
| 4 | User | N | assigned to | N | Market | Market agents are assigned to the markets they may work. |
| 5 | Market | 1 | offers | N | Market Offering | A market lists many offerings. |
| 6 | Market Offering | N | listed as | 1 | Product | Each offering is one catalog product sold at one market. |
| 7 | Product | N | classifies | 1 | Product Category | Every product belongs to a category. |
| 8 | Market Offering | 1 | records | N | Price History | Each price change appends a history row. |
| 9 | Restaurant | 1 | schedules | N | Scheduled Order | A restaurant may run several recurring schedules. |
| 10 | Scheduled Order | 1 | generates | N | Order | A schedule produces one real order per occurrence. |
| 11 | Restaurant | 1 | places | N | Order | Orders belong to exactly one restaurant. |
| 12 | Order | 1 | contains | N | Order Item | An order is made of one or more lines. |
| 13 | Market Offering | 1 | selected in | N | Order Item | A line points at the offering that was bought. |
| 14 | Order | 1 | raises | N | Order Issue | An order may accumulate several reported issues. |
| 15 | Order Item | 0..1 | on item | N | Order Issue | An issue may pin down the specific line affected. |
| 16 | Restaurant | 1 | has credit | 1 | Credit Account | One credit account per restaurant. |
| 17 | Credit Account | 1 | records | N | Credit Transaction | Every movement is written on the account. |
| 18 | Order | 1 | charged by | N | Credit Transaction | Charges, refunds and adjustments trace back to an order. |
| 19 | Credit Account | 1 | summarized in | N | Credit Statement | Monthly statements roll up the account. |
| 20 | Order | N | batched into | 1 | Procurement Batch | Confirmed orders of a day are batched for buying. |
| 21 | Procurement Batch | 1 | contains | N | Procurement Item | A batch is a shopping list of aggregated items. |
| 22 | Market Offering | 1 | procured as | N | Procurement Item | Each item names the offering to buy. |
| 23 | Procurement Batch | N | received at | 1 | Hub | Purchased goods are handed off to one hub. |
| 24 | Hub | 1 | stocks | N | Hub Inventory | A hub holds stock per offering. |
| 25 | Market Offering | 1 | stocked as | N | Hub Inventory | Stock rows are keyed by offering. |
| 26 | Hub | 1 | dispatches | N | Delivery Route | Routes depart from a hub. |
| 27 | Vehicle | 1 | assigned | N | Delivery Route | A vehicle serves many routes over time. |
| 28 | Driver | 1 | drives | N | Delivery Route | A driver runs many routes over time. |
| 29 | Delivery Route | 1 | includes | N | Delivery | A route is an ordered list of deliveries. |
| 30 | Order | 1 | fulfilled by | 0..1 | Delivery | An order gets a delivery once it is dispatched. |
| 31 | Delivery Address | 0..1 | delivered to | N | Order | An order names the address it ships to. |
| 32 | Market | 0..1 | sourced from | N | Order | An order is sourced from a single market. |
| 33 | Market | 0..1 | targets | N | Scheduled Order | A schedule targets a single market. |
| 34 | Delivery Address | 0..1 | defaults to | N | Scheduled Order | A schedule carries a default shipping address. |
| 35 | Market | 1 | opens | N | Market Session | A market opens one session per service date. |
| 36 | Market Session | 1 | scopes | N | Order | A session gathers the orders of its date. |
| 37 | Market Session | 1 | runs | N | Procurement Batch | A session runs the buying batches. |
| 38 | Market Session | 1 | plans | N | Delivery Route | A session plans the routes for its goods. |
| 39 | Order | 1 | invoiced by | 0..1 | Invoice | A delivered order is invoiced once. |
| 40 | Invoice | 1 | itemized as | N | Invoice Line | An invoice is broken into goods and fee lines. |

---

## 3. Scope note (from the diagram legend)

Excluded from the conceptual model: roles, notifications, units of measurement, credit statement lines,
auth tokens, audit logs, caches, device registrations, assignment/link tables, and implementation-only
event/detail tables (hub inbound/outbound/handover events, hub discrepancies, cross-dock transfers,
sorting progress, route plans, order claims, favorites, tags, packing codes, settings).
