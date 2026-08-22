# FreshFlow Implemented Database ERD

**Reconciled:** 2026-08-16

**Source of truth:** `src/FreshFlow.Infrastructure.Persistence/Migrations/AppDbContextModelSnapshot.cs`

**Schema baseline:** through `20260814090643_AddOrderItemPackingWeightSnapshot`

This document maps the database used by the current backend. Logical names are normalized to
`snake_case`; use the EF snapshot/migrations for exact physical column casing, types, indexes,
defaults, check constraints, and delete behavior.

## Reconciliation with the previous ERD

| Module | Previous ERD | Current model | Delta |
|---|---:|---:|---:|
| Auth | 9 | 9 | 0 |
| Catalog | 4 | 5 | +1 |
| Pricing | 2 | 4 | +2 |
| Orders | 6 | 12 | +6 |
| Procurement | 0 | 7 | +7 |
| Hub | 0 | 10 | +10 |
| Logistics | 0 | 6 | +6 |
| Invoicing | 0 | 2 | +2 |
| Notifications | 0 | 2 | +2 |
| Platform | 1 | 2 | +1 |
| **Total** | **22** | **59** | **+37** |

The current EF model contains **42 enforced foreign keys**. Cross-module identifiers are mostly
intentional application-level references and therefore do not create database FK constraints.

Legend:

- Solid line (`--`): FK enforced by the current EF/database model.
- Dotted line (`..`): logical relationship only; no current database FK.
- `PK`, `FK`, and `UK`: primary key, enforced foreign key, and unique key/index.
- Audit-user fields such as `created_by`, `updated_by`, `recorded_by`, and `assigned_by` remain
  visible as attributes where useful but their dotted edges are omitted to keep diagrams readable.

## 1. Auth

```mermaid
erDiagram
    roles ||--o{ users : role_id
    users ||--o{ refresh_tokens : user_id
    refresh_tokens |o--o{ refresh_tokens : replaced_by_token_id
    users ||--o{ password_reset_tokens : user_id
    users ||--o{ verification_codes : user_id
    users ||--o{ user_market_assignments : user_id
    markets ||..o{ user_market_assignments : market_id
    users ||..o| driver_profiles : user_id
    users ||..o| restaurants : user_id
    restaurants ||--o{ delivery_addresses : restaurant_id

    roles {
        uuid id PK
        string name UK
        string description
    }

    users {
        uuid id PK
        uuid role_id FK
        string email UK
        string phone UK
        string full_name
        boolean is_active
        datetime locked_until
        datetime deleted_at
    }

    refresh_tokens {
        uuid id PK
        uuid user_id FK
        uuid replaced_by_token_id FK
        uuid family_id
        datetime expires_at
        datetime revoked_at
    }

    password_reset_tokens {
        uuid id PK
        uuid user_id FK
        datetime expires_at
        datetime used_at
    }

    verification_codes {
        uuid id PK
        uuid user_id FK
        string channel
        datetime expires_at
        datetime used_at
    }

    user_market_assignments {
        uuid id PK
        uuid user_id FK
        uuid market_id
        uuid assigned_by
    }

    driver_profiles {
        uuid id PK
        uuid user_id UK
        string license_plate
        string phone_number
    }

    restaurants {
        uuid id PK
        uuid user_id UK
        string name
        string status
        string tax_code
        string invoice_email
        time pickup_start
        time pickup_end
    }

    delivery_addresses {
        uuid id PK
        uuid restaurant_id FK
        string recipient_name
        string phone
        string address_line
        decimal latitude
        decimal longitude
        boolean is_default
    }

    markets {
        uuid id PK
    }
```

## 2. Catalog and Pricing

```mermaid
erDiagram
    product_categories |o--o{ product_categories : parent_id
    product_categories |o--o{ products : category_id
    packing_codes |o--o{ products : packing_code_id
    units_of_measurement ||--o{ products : unit_id
    markets ||..o{ market_products : market_id
    products ||..o{ market_products : product_id
    market_products ||--o{ price_snapshots : market_product_id
    market_products ||--o{ market_product_tags : market_product_id
    tags ||--o{ market_product_tags : tag_id

    markets {
        uuid id PK
        string code
        string name
        string address
        decimal latitude
        decimal longitude
        boolean is_active
        datetime deleted_at
    }

    product_categories {
        uuid id PK
        uuid parent_id FK
        string name UK
        string image_url
        boolean is_active
        datetime deleted_at
    }

    packing_codes {
        uuid id PK
        string code UK
        decimal capacity_kg
        boolean is_active
        datetime deleted_at
    }

    units_of_measurement {
        uuid id PK
        string name UK
        string abbreviation
        boolean is_active
        datetime deleted_at
    }

    products {
        uuid id PK
        uuid category_id FK
        uuid packing_code_id FK
        uuid unit_id FK
        string name
        int minimum_order_quantity
        string vat_rate
        datetime deleted_at
    }

    market_products {
        uuid id PK
        uuid market_id
        uuid product_id
        decimal current_price
        int current_quantity
        int reserved_quantity
        datetime deleted_at
    }

    price_snapshots {
        uuid id PK
        uuid market_product_id FK
        decimal price
        int quantity
        uuid recorded_by
        datetime recorded_at
    }

    tags {
        uuid id PK
        string name UK
        boolean pins_to_top
        uuid updated_by
        datetime deleted_at
    }

    market_product_tags {
        uuid market_product_id PK, FK
        uuid tag_id PK, FK
    }
```

## 3. Orders and Invoicing

```mermaid
erDiagram
    restaurants ||..o{ orders : restaurant_id
    delivery_addresses |o..o{ orders : delivery_address_id
    market_sessions |o..o{ orders : market_session_id
    scheduled_orders |o..o{ orders : scheduled_order_id
    orders ||--o{ order_items : order_id
    market_products ||..o{ order_items : market_product_id

    restaurants ||..o{ scheduled_orders : restaurant_id
    delivery_addresses |o..o{ scheduled_orders : delivery_address_id
    markets |o..o{ scheduled_orders : market_id
    scheduled_orders ||--o{ scheduled_order_items : scheduled_order_id
    market_products ||..o{ scheduled_order_items : market_product_id

    orders ||--o{ order_issues : order_id
    order_items |o--o{ order_issues : order_item_id
    orders ||--o{ order_claims : order_id
    credit_transactions |o--o| order_claims : refund_transaction_id

    restaurants ||..o| restaurant_credit : restaurant_id
    restaurants ||..o{ credit_transactions : restaurant_id
    orders |o..o{ credit_transactions : order_id
    restaurants ||..o{ credit_statements : restaurant_id
    credit_statements ||--o{ credit_statement_lines : credit_statement_id
    credit_transactions ||..o{ credit_statement_lines : transaction_id
    orders |o..o{ credit_statement_lines : order_id

    restaurants ||..o{ restaurant_favorites : restaurant_id
    market_products ||..o{ restaurant_favorites : market_product_id

    orders ||..o| invoices : order_id
    restaurants ||..o{ invoices : restaurant_id
    invoices ||--o{ invoice_lines : invoice_id

    orders {
        uuid id PK
        uuid restaurant_id
        uuid delivery_address_id
        uuid market_id
        uuid market_session_id
        uuid scheduled_order_id
        string status
        string payment_status
        decimal subtotal_amount
        decimal vat_amount
        decimal delivery_fee
        decimal total_amount
        datetime scheduled_for
        datetime deleted_at
    }

    order_items {
        uuid id PK
        uuid order_id FK
        uuid market_product_id
        string product_name_snapshot
        string packing_code_snapshot
        decimal packing_weight_kg_snapshot
        int quantity
        decimal unit_price
        decimal actual_quantity
        decimal actual_unit_price
    }

    scheduled_orders {
        uuid id PK
        uuid restaurant_id
        uuid delivery_address_id
        uuid market_id
        string recurrence_type
        datetime first_run_at
        datetime last_executed_at
        datetime cancelled_at
        datetime deleted_at
    }

    scheduled_order_items {
        uuid id PK
        uuid scheduled_order_id FK
        uuid market_product_id
        int quantity
    }

    order_issues {
        uuid id PK
        uuid order_id FK
        uuid order_item_id FK
        uuid reported_by
        string issue_type
        decimal affected_quantity
        string status
        datetime resolved_at
        datetime deleted_at
    }

    order_claims {
        uuid id PK
        uuid order_id FK
        uuid restaurant_id
        uuid refund_transaction_id FK, UK
        string reason
        decimal amount
        string status
        datetime reviewed_at
        datetime deleted_at
    }

    restaurant_credit {
        uuid restaurant_id PK
        decimal credit_limit
        decimal outstanding_balance
        string last_alerted_level
    }

    credit_transactions {
        uuid id PK
        uuid restaurant_id
        uuid order_id
        uuid recorded_by_user_id
        string type
        decimal amount
        decimal balance_after
        string payment_method
        datetime created_at
    }

    credit_statements {
        uuid id PK
        uuid restaurant_id
        datetime period_start
        datetime period_end
        decimal opening_balance
        decimal closing_balance
        datetime generated_at
    }

    credit_statement_lines {
        uuid id PK
        uuid credit_statement_id FK
        uuid transaction_id
        uuid order_id
        string type
        decimal amount
        decimal balance_after
        datetime occurred_at
    }

    restaurant_favorites {
        uuid id PK
        uuid restaurant_id
        uuid market_product_id
        datetime created_at
    }

    operational_settings {
        uuid id PK
        time daily_cutoff_time
        int delivery_window_days
        boolean batching_enabled
        decimal base_fee
        decimal delivery_fee_per_km
        decimal minimum_fee
        decimal rounding_unit
        string default_route_type
    }

    invoices {
        uuid id PK
        uuid order_id UK
        uuid restaurant_id
        string number
        string serial
        string status
        decimal sub_total
        decimal vat_amount
        decimal total
        datetime issued_at
        datetime deleted_at
    }

    invoice_lines {
        uuid id PK
        uuid invoice_id FK
        string product_name
        decimal quantity
        decimal unit_price
        decimal line_total
    }

    restaurants {
        uuid id PK
    }

    delivery_addresses {
        uuid id PK
    }

    markets {
        uuid id PK
    }

    market_sessions {
        uuid id PK
    }

    market_products {
        uuid id PK
    }
```

## 4. Procurement

```mermaid
erDiagram
    markets ||..o{ market_sessions : market_id
    hubs |o..o{ market_sessions : hub_id
    market_sessions ||--o{ market_session_agents : session_id
    users ||..o{ market_session_agents : user_id
    market_sessions ||--o{ market_session_vehicles : session_id
    vehicles ||..o{ market_session_vehicles : vehicle_id

    market_sessions |o--o{ procurement_batches : market_session_id
    markets ||..o{ procurement_batches : market_id
    hubs |o..o{ procurement_batches : hub_id
    users |o..o{ procurement_batches : assigned_agent_user_id
    procurement_batches ||--o{ procurement_batch_items : procurement_batch_id
    procurement_batches ||--o{ procurement_batch_orders : procurement_batch_id
    procurement_batches ||--o{ procurement_exceptions : procurement_batch_id
    market_products ||..o{ procurement_batch_items : market_product_id
    users |o..o{ procurement_batch_items : assigned_agent_user_id
    orders ||..o| procurement_batch_orders : order_id
    market_products ||..o{ procurement_exceptions : market_product_id

    market_sessions {
        uuid id PK
        uuid market_id
        uuid hub_id
        date service_date
        datetime closes_at
        string status
        decimal planned_capacity_kg
        datetime batching_completed_at
        datetime closed_at
        datetime deleted_at
    }

    market_session_agents {
        uuid session_id PK, FK
        uuid user_id PK
        datetime assigned_at
        uuid assigned_by
    }

    market_session_vehicles {
        uuid session_id PK, FK
        uuid vehicle_id PK
        datetime assigned_at
        uuid assigned_by
    }

    procurement_batches {
        uuid id PK
        uuid market_session_id FK
        uuid market_id
        uuid hub_id
        uuid assigned_agent_user_id
        string code UK
        date batch_date
        string status
        int total_item_count
        datetime completed_at
        datetime handed_off_at
        datetime deleted_at
    }

    procurement_batch_items {
        uuid id PK
        uuid procurement_batch_id FK
        uuid market_product_id
        uuid assigned_agent_user_id
        string product_name_snapshot
        int total_quantity
        int actual_quantity
        decimal reference_unit_price
        decimal actual_unit_price
        datetime purchased_at
        datetime deleted_at
    }

    procurement_batch_orders {
        uuid id PK
        uuid procurement_batch_id FK
        uuid order_id UK
        datetime deleted_at
    }

    procurement_exceptions {
        uuid id PK
        uuid procurement_batch_id FK
        uuid market_product_id
        uuid reported_by_user_id
        string type
        int reported_quantity
        string proof_image_url
        datetime reported_at
        datetime deleted_at
    }

    markets {
        uuid id PK
    }

    hubs {
        uuid id PK
    }

    users {
        uuid id PK
    }

    vehicles {
        uuid id PK
    }

    market_products {
        uuid id PK
    }

    orders {
        uuid id PK
    }
```

## 5. Hub

```mermaid
erDiagram
    markets |o..o{ hubs : market_id
    users |o..o{ hubs : managed_by
    hubs ||--o{ hub_driver_assignments : hub_id
    users ||..o{ hub_driver_assignments : user_id
    hubs ||--o{ hub_staff_assignments : hub_id
    users ||..o{ hub_staff_assignments : user_id

    hubs ||--o{ hub_inbound_events : hub_id
    delivery_routes |o..o{ hub_inbound_events : delivery_route_id
    markets |o..o{ hub_inbound_events : source_market_id
    hubs ||--o{ hub_inventory : hub_id
    market_products ||..o{ hub_inventory : market_product_id
    hubs ||--o{ hub_outbound_events : hub_id
    delivery_routes ||..o{ hub_outbound_events : destination_route_id

    hubs ||--o{ cross_dock_transfers : hub_id
    hub_inbound_events ||--o{ cross_dock_transfers : inbound_event_id
    delivery_routes ||..o{ cross_dock_transfers : outbound_route_id

    hubs ||--o{ hub_discrepancies : hub_id
    hub_inbound_events ||--o{ hub_discrepancies : inbound_event_id
    orders ||..o{ hub_discrepancies : order_id
    order_items ||..o{ hub_discrepancies : order_item_id

    hubs ||--o{ hub_handover_events : hub_id
    hub_outbound_events |o--o{ hub_handover_events : outbound_event_id
    delivery_routes ||..o{ hub_handover_events : delivery_route_id

    hubs ||..o{ hub_sorting_progress : hub_id
    delivery_routes |o..o{ hub_sorting_progress : route_id
    order_items ||..o{ hub_sorting_progress : order_item_id

    hubs {
        uuid id PK
        uuid market_id
        uuid managed_by
        string name
        string address
        decimal capacity_kg
        decimal occupied_capacity_kg
        boolean is_active
        datetime deleted_at
    }

    hub_driver_assignments {
        uuid hub_id PK, FK
        uuid user_id PK
    }

    hub_staff_assignments {
        uuid hub_id PK, FK
        uuid user_id PK
    }

    hub_inbound_events {
        uuid id PK
        uuid hub_id FK
        uuid delivery_route_id
        uuid source_market_id
        string status
        string condition_status
        decimal total_quantity_kg
        json items
        datetime arrived_at
        datetime deleted_at
    }

    hub_inventory {
        uuid id PK
        uuid hub_id FK
        uuid market_product_id
        decimal quantity_available
        decimal quantity_in
        decimal quantity_out
        datetime recorded_at
        datetime deleted_at
    }

    hub_outbound_events {
        uuid id PK
        uuid hub_id FK
        uuid destination_route_id
        decimal total_quantity_kg
        json items
        datetime dispatched_at
        datetime deleted_at
    }

    cross_dock_transfers {
        uuid id PK
        uuid hub_id FK
        uuid inbound_event_id FK
        uuid outbound_route_id
        string status
        datetime deleted_at
    }

    hub_discrepancies {
        uuid id PK
        uuid hub_id FK
        uuid inbound_event_id FK
        uuid order_id
        uuid order_item_id
        string condition_status
        decimal affected_quantity
        string status
        datetime deleted_at
    }

    hub_handover_events {
        uuid id PK
        uuid hub_id FK
        uuid outbound_event_id FK
        uuid delivery_route_id
        uuid driver_user_id
        string status
        datetime handed_over_at
        datetime driver_confirmed_at
        datetime deleted_at
    }

    hub_sorting_progress {
        uuid id PK
        uuid hub_id
        uuid route_id
        uuid order_item_id
        date service_date
        decimal sorted_quantity_kg
        string status
        datetime sorted_at
        datetime deleted_at
    }

    markets {
        uuid id PK
    }

    users {
        uuid id PK
    }

    delivery_routes {
        uuid id PK
    }

    market_products {
        uuid id PK
    }

    orders {
        uuid id PK
    }

    order_items {
        uuid id PK
    }
```

## 6. Logistics

```mermaid
erDiagram
    market_sessions ||..o{ route_plans : market_session_id
    hubs ||..o{ route_plans : hub_id
    route_plans |o--o{ delivery_routes : route_plan_id
    market_sessions ||..o{ delivery_routes : market_session_id
    hubs |o..o{ delivery_routes : hub_id
    vehicles |o..o{ delivery_routes : vehicle_id
    users |o..o{ delivery_routes : driver_user_id
    delivery_routes ||--o{ deliveries : delivery_route_id
    orders ||..o| deliveries : order_id
    deliveries ||--o{ delivery_issues : delivery_id
    users ||..o{ delivery_issues : reported_by
    hubs |o..o{ vehicles : hub_id

    route_plans {
        uuid id PK
        uuid market_session_id
        uuid hub_id
        date service_date
        string status
        string optimization_criteria
        decimal total_distance_km
        decimal total_load_kg
        int vehicles_used
        json unassigned
        datetime deleted_at
    }

    delivery_routes {
        uuid id PK
        uuid route_plan_id FK
        uuid market_session_id
        uuid hub_id
        uuid vehicle_id
        uuid suggested_vehicle_id
        uuid driver_user_id
        date service_date
        string route_type
        string status
        json stops
        decimal total_distance_km
        decimal planned_load_kg
        datetime deleted_at
    }

    deliveries {
        uuid id PK
        uuid delivery_route_id FK
        uuid order_id UK
        int sequence_number
        string status
        datetime estimated_arrival
        datetime actual_arrival
        string proof_url
        datetime deleted_at
    }

    delivery_issues {
        uuid id PK
        uuid delivery_id FK
        uuid reported_by
        string issue_type
        string status
        datetime deleted_at
    }

    vehicles {
        uuid id PK
        uuid hub_id
        string plate_number UK
        string vehicle_type
        decimal capacity_kg
        boolean is_available
        datetime deleted_at
    }

    route_matrix_cache {
        string pair_key PK
        string profile
        decimal from_latitude
        decimal from_longitude
        decimal to_latitude
        decimal to_longitude
        long distance_meters
        long duration_seconds
        datetime calculated_at
    }

    market_sessions {
        uuid id PK
    }

    hubs {
        uuid id PK
    }

    users {
        uuid id PK
    }

    orders {
        uuid id PK
    }
```

## 7. Notifications and Platform

```mermaid
erDiagram
    users ||..o{ notifications : user_id
    users ||..o{ notification_devices : user_id
    users ||..o{ assistant_conversations : user_id
    markets |o..o{ assistant_conversations : market_id
    users |o..o{ audit_logs : actor_id

    notifications {
        uuid id PK
        uuid user_id
        string type
        string title
        string body
        json payload
        boolean is_read
        string send_status
        int attempt_count
        datetime read_at
        datetime deleted_at
    }

    notification_devices {
        uuid id PK
        uuid user_id
        string device_id
        string platform
        string token
        datetime revoked_at
        datetime deleted_at
    }

    assistant_conversations {
        uuid id PK
        uuid user_id
        uuid market_id
        string session_id UK
        json state_json
        datetime expires_at
    }

    audit_logs {
        uuid id PK
        uuid actor_id
        string action
        string entity_type
        uuid entity_id
        json details
        datetime occurred_at
    }

    users {
        uuid id PK
    }

    markets {
        uuid id PK
    }
```

## Important implementation boundaries

- `hub_sorting_progress.hub_id` is not an enforced FK even though it points to `hubs`.
- Orders, Pricing, Procurement, Hub, Logistics, Notifications, Invoicing, and Platform use many
  cross-module IDs without database FKs; the dotted edges above reflect that boundary.
- `pricing_settings` is not part of the current model; it was dropped by
  `20260813074950_DropPricingSettings`. `operational_settings` remains the active singleton.
- Keyless EF query rows used by Analytics and cross-module readers are SQL projections, not tables,
  and are intentionally excluded from this ERD.
