# FreshFlow Implemented Database ERD

**Source of truth:** `docs/database/03-database-schema.logical.dbml` and EF Core migrations/model snapshot.
**Scope:** implemented tables only. Planned tables are intentionally omitted.

Legend:

- Solid relationship: implemented DB relationship or enforced FK.
- Dotted relationship: logical application relationship that is not necessarily enforced by the current database schema.
- Audit-user references (`created_by`, `updated_by`, `recorded_by`, `assigned_by`) are shown as attributes only and deliberately not drawn as relationships (see the logical model for them).

```mermaid
erDiagram
    roles ||--o{ users : role_id
    users ||--o{ refresh_tokens : user_id
    refresh_tokens |o--o| refresh_tokens : replaced_by_token_id
    users ||--o{ password_reset_tokens : user_id
    users ||--o{ verification_codes : user_id
    users ||--o{ user_market_assignments : user_id
    markets ||..o{ user_market_assignments : market_id
    users ||..o| driver_profiles : driver_profile
    users ||..o| restaurants : restaurant_profile

    restaurants ||--o{ delivery_addresses : restaurant_id
    restaurants ||--o{ orders : restaurant_id
    restaurants ||--o{ scheduled_orders : restaurant_id
    restaurants ||--o| restaurant_credit : restaurant_id
    restaurants ||--o{ credit_transactions : restaurant_id

    product_categories |o--o{ products : category_id
    units_of_measurement ||--o{ products : unit_id
    markets ||..o{ market_products : market_id
    products ||..o{ market_products : product_id
    market_products ||--o{ price_snapshots : market_product_id
    market_products ||--o{ order_items : market_product_id

    scheduled_orders ||..o{ orders : scheduled_order_id
    orders ||--o{ order_items : order_id
    orders |o--o{ credit_transactions : order_id
    orders ||--o{ order_issues : order_id
    order_items |o--o{ order_issues : order_item_id
    users ||--o{ order_issues : reported_by

    users ||..o{ assistant_conversations : user_id
    markets ||..o{ assistant_conversations : market_id

    roles {
        uuid id PK
        string name UK
        string description
    }

    users {
        uuid id PK
        string email UK
        string phone UK
        string full_name
        string avatar_url
        uuid role_id FK
        boolean is_active
        datetime email_verified_at
    }

    refresh_tokens {
        uuid id PK
        uuid user_id FK
        uuid replaced_by_token_id FK
        uuid family_id
        datetime expires_at
        datetime revoked_at
        string revoked_reason
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
        uuid market_id FK
        uuid assigned_by
    }

    driver_profiles {
        uuid id PK
        uuid user_id FK
        string license_plate
        string phone_number
    }

    restaurants {
        uuid id PK
        uuid user_id FK
        string name
        string address
        string contact_person
        time pickup_start
        time pickup_end
        string status
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

    product_categories {
        uuid id PK
        string name UK
        boolean is_active
    }

    units_of_measurement {
        uuid id PK
        string name UK
        string abbreviation
        boolean is_active
    }

    products {
        uuid id PK
        uuid category_id FK
        uuid unit_id FK
        string name
        string description
        uuid created_by
    }

    markets {
        uuid id PK
        string name
        string location
        string address
        decimal latitude
        decimal longitude
        boolean is_active
    }

    market_products {
        uuid id PK
        uuid market_id FK
        uuid product_id FK
        money current_price
        int current_quantity
        int reserved_quantity
        uuid updated_by
    }

    price_snapshots {
        uuid id PK
        uuid market_product_id FK
        money price
        int quantity
        uuid recorded_by
        datetime recorded_at
    }

    orders {
        uuid id PK
        uuid restaurant_id FK
        uuid order_group_id
        uuid scheduled_order_id FK
        string status
        string payment_status
        datetime scheduled_for
        datetime confirmed_receipt_at
        money total_amount
        string notes
        datetime cancelled_at
        string cancellation_reason
    }

    order_items {
        uuid id PK
        uuid order_id FK
        uuid market_product_id FK
        string product_name_snapshot
        int quantity
        money unit_price
        money locked_unit_price
        money locked_total
        decimal actual_quantity
    }

    scheduled_orders {
        uuid id PK
        uuid restaurant_id FK
        string recurrence_type
        datetime first_run_at
        datetime last_executed_at
        datetime cancelled_at
        string notes
    }

    restaurant_credit {
        uuid restaurant_id PK
        money credit_limit
        money outstanding_balance
    }

    credit_transactions {
        uuid id PK
        uuid restaurant_id FK
        uuid order_id FK
        string type
        money amount
        money balance_after
        string note
        datetime created_at
    }

    order_issues {
        uuid id PK
        uuid order_id FK
        uuid order_item_id FK
        string issue_type
        decimal affected_quantity
        string description
        string status
        uuid reported_by FK
        datetime resolved_at
    }

    assistant_conversations {
        uuid id PK
        uuid user_id FK
        uuid market_id FK
        string session_id UK
        json state
        datetime expires_at
    }
```
