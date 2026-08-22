# FreshFlow — Entities Description

> This table describes the 59 application entities mapped in the current EF Core model. The EF Core technical table `__EFMigrationsHistory` is not included.

| # | Entity | Description |
|---:|---|---|
| 1 | Role | Defines a user's permissions and responsibilities in the system. |
| 2 | User | Represents an account used to access FreshFlow. |
| 3 | Refresh Token | Stores a renewable authentication token and its rotation history. |
| 4 | Password Reset Token | Stores a one-time token used to reset a user's password. |
| 5 | Verification Code | Stores a temporary code used to verify a user's email address or phone number. |
| 6 | User Market Assignment | Links a market agent to a market they are allowed to manage. |
| 7 | Driver Profile | Stores additional information about a delivery driver. |
| 8 | Restaurant | Represents a restaurant that purchases products through FreshFlow. |
| 9 | Delivery Address | Stores a restaurant's receiving and delivery location. |
| 10 | Product Category | Classifies products and supports hierarchical product grouping. |
| 11 | Unit of Measurement | Defines the measurement unit used for a product, such as kilogram or box. |
| 12 | Packing Code | Defines a supported product packing option and its standard weight. |
| 13 | Product | Represents a product in the system-wide catalog. |
| 14 | Market | Represents a wholesale market where products are sourced. |
| 15 | Market Product | Represents a product offered at a specific market with its current price and availability. |
| 16 | Tag | Defines a reusable label used to classify or highlight market products. |
| 17 | Market Product Tag | Links a market product to a tag. |
| 18 | Price Snapshot | Records a historical price and quantity update for a market product. |
| 19 | Restaurant Favorite | Stores a market product saved as a favorite by a restaurant. |
| 20 | Order | Represents a purchase request placed by a restaurant. |
| 21 | Order Item | Represents a product, quantity, price, and packing information within an order. |
| 22 | Scheduled Order | Defines a recurring order that is generated automatically. |
| 23 | Scheduled Order Item | Represents a product and quantity configured in a scheduled order. |
| 24 | Restaurant Credit | Stores a restaurant's credit limit and current outstanding balance. |
| 25 | Credit Transaction | Records a charge, settlement, refund, or adjustment to restaurant credit. |
| 26 | Credit Statement | Summarizes a restaurant's credit activity within a statement period. |
| 27 | Credit Statement Line | Records an individual credit transaction included in a credit statement. |
| 28 | Order Issue | Records a missing, incorrect, or damaged product reported for an order. |
| 29 | Order Claim | Represents a restaurant's monetary claim and its approval or rejection result. |
| 30 | Operational Settings | Stores system-wide operational rules such as order cutoff and credit thresholds. |
| 31 | Procurement Batch | Groups confirmed orders into a single procurement operation. |
| 32 | Procurement Batch Order | Links an order to the procurement batch responsible for fulfilling it. |
| 33 | Procurement Batch Item | Represents a product and quantity to be purchased in a procurement batch. |
| 34 | Procurement Exception | Records a procurement problem such as unavailable stock or a price difference. |
| 35 | Market Session | Represents a scheduled purchasing session at a market for a service date and hub. |
| 36 | Market Session Agent | Assigns a market agent to a market session. |
| 37 | Market Session Vehicle | Assigns a vehicle to a market session. |
| 38 | Hub | Represents a distribution hub where goods are received, sorted, and dispatched. |
| 39 | Hub Staff Assignment | Assigns a staff member to work at a hub. |
| 40 | Hub Driver Assignment | Assigns a driver to a hub for a service date. |
| 41 | Hub Inbound Event | Records goods received by a hub from a procurement operation. |
| 42 | Hub Inventory | Tracks the quantity of each market product available at a hub. |
| 43 | Hub Sorting Progress | Tracks the sorting status and sorted quantity of an order item at a hub. |
| 44 | Hub Discrepancy | Records missing, excess, or damaged goods discovered at a hub. |
| 45 | Cross-Dock Transfer | Records goods transferred directly from inbound receiving to outbound delivery. |
| 46 | Hub Outbound Event | Records goods dispatched from a hub for delivery. |
| 47 | Hub Handover Event | Records the transfer of goods from hub staff to a driver. |
| 48 | Vehicle | Represents a vehicle used for procurement or delivery. |
| 49 | Route Plan | Stores a proposed or approved delivery plan for a market session and hub. |
| 50 | Delivery Route | Represents an assigned route containing an ordered list of delivery stops. |
| 51 | Delivery | Tracks the delivery of an order to a restaurant. |
| 52 | Delivery Issue | Records a problem encountered during delivery. |
| 53 | Route Matrix Cache | Caches calculated travel times and distances between route locations. |
| 54 | Invoice | Represents a tax invoice issued for an order. |
| 55 | Invoice Line | Represents an individual product or charge shown on an invoice. |
| 56 | Notification Device | Stores a user device registered to receive push notifications. |
| 57 | Notification | Stores a message sent to a user about a system or business event. |
| 58 | Assistant Conversation | Stores a conversation between a user and the FreshFlow AI assistant. |
| 59 | Audit Log | Records important system actions for monitoring and accountability. |
