## **(*) 3.1. Capstone Project name**

### **English:**

FreshFlow – An Intermediary Platform for Food Procurement and Logistics Optimization from Wholesale Markets for Restaurants in Ho Chi Minh City

### **Vietnamese:**

FreshFlow – Nền tảng trung gian thu mua và tối ưu vận chuyển thực phẩm từ chợ đầu mối cho các nhà hàng tại TPHCM

### **Abbreviation:**

**FFX**

---

> **Implementation alignment note (updated 2026-06-19).** This registration is the original
> proposal. The following business-model decisions were made during design/implementation and now
> govern the actual project (see `docs/06-context-decisions.md`):
> - **Market Agents are internal FreshFlow employees**, not external market vendors. There is no
>   public/vendor self-registration. (DEC-001)
> - **Roles** are: Admin, Market Agent, Restaurant, Hub Staff, Driver (Operations Manager is merged
>   into Admin). The original "Kiosk Staff" is retained only as a legacy alias of Market Agent.
>   (DEC-003/004/005)
> - **Payment uses a B2B credit / công nợ model**, not an online payment gateway. Confirming an order
>   draws down the restaurant's Admin-set credit limit; debt is settled out-of-band. (DEC-002)
> - A dedicated **product Catalog** capability (products, categories, units, markets) underpins
>   Pricing and Orders.
> - Market Agents may optionally update price/quantity by **voice (speech-to-text)** with a mandatory
>   read-back confirmation before persisting; this reuses the existing price/quantity commands and
>   per-market authorization (see Functional Requirement §1).
> - The AI direction changed from **AI Price Prediction** (forecasting prices) to an **AI Shopping
>   Assistant** — a natural-language chat layer that orchestrates existing backend functions. See
>   `docs/SURVEY-2026-06-18-ai-shopping-assistant-feasibility.md`. Price forecasting is no longer in
>   scope.
> - Modules currently implemented: **Auth, Catalog, Pricing, Orders (incl. Credit)**. Logistics
>   Optimization (VRP), Hub Management, Analytics, and the AI Shopping Assistant remain proposed/future scope.

---

# **Context**

In wholesale food markets such as Ho Chi Minh City’s major hubs (Hoc Mon, Binh Dien, Thu Duc), pricing and supply fluctuate continuously throughout the day. Restaurants and small food businesses often face significant challenges in procurement and logistics management.

Key challenges include:

- **Lack of real-time pricing visibility:** Restaurants do not have access to up-to-date prices from wholesale markets.
- **Manual procurement process:** Businesses must physically visit markets, leading to inefficiency and time consumption.
- **Fragmented logistics:** Transportation from markets to restaurants is unoptimized, causing high delivery costs.
- **Inefficient routing and scheduling:** No system exists to optimize delivery routes, vehicle usage, or delivery schedules.
- **Supply-demand mismatch:** Lack of coordination between supply sources and restaurant demand.
- **No centralized distribution system:** Absence of hub-based aggregation and redistribution.

---

# **Proposed Solutions**

This project proposes **FreshFlow**, a real-time B2B procurement and logistics optimization platform that connects wholesale markets, distribution hubs, and restaurants into a unified supply chain system.

Key components include:

- **Market Agent System:**
Enable internal FreshFlow Market Agents stationed at wholesale markets to update product prices and quantities in real time. (Market Agents are FreshFlow employees, not external vendors — DEC-001.)
- **Restaurant Procurement Platform:**
Allow restaurants to view real-time prices, place bulk orders, and schedule recurring purchases.
- **Hub-based Distribution System:**
Aggregate goods from markets, support cross-docking, and redistribute to restaurants efficiently.
- **Logistics Optimization Engine:**
Optimize routes, schedules, and vehicle allocation to reduce delivery costs.
- **AI Shopping Assistant (Optional Advanced):**
A natural-language chat assistant that helps restaurants search products, build and edit orders, and reorder from history by orchestrating existing backend functions. The AI performs intent recognition and conversation only — it never accesses the database directly and never auto-confirms; the user must explicitly confirm before any order is placed.

---

# **Functional Requirements**

---

## **1. Product Catalog & Real-Time Pricing Management**

- The system shall maintain a system-wide product catalog (products, categories, units) and the set of wholesale markets. Admin manages catalog definitions; Market Agents may only update price and quantity for existing catalog products.
- The system shall allow internal Market Agents (FreshFlow employees) to update product prices and available quantities at their assigned market.
- The system should allow Market Agents to update prices and quantities by **voice** (speech-to-text) for hands-free, fast updates in the noisy market environment. The system shall read back the parsed product, price, and quantity and require explicit confirmation before persisting; voice updates reuse the same price/quantity commands and authorization (an Agent can only update their assigned market).
- The system shall synchronize price updates in real time to all connected clients.
- The system shall maintain historical pricing data for analysis.
- The system shall notify users when significant price changes occur.

---

## **2. Order Management System**

- The system shall allow restaurants to create and manage bulk orders.
- The system shall support scheduled orders (daily/weekly).
- The system shall allow users to track order status in real time.
- The system shall support order grouping (auto-batching at the daily cutoff) for logistics optimization.
- The system shall settle orders via a **B2B credit / công nợ** model: each restaurant has an Admin-set credit limit; confirming an order draws down available credit, and outstanding debt is settled out-of-band and recorded by Admin. (No online payment gateway — DEC-002.)

---

## **3. Logistics Optimization System**

- The system shall calculate delivery routes from Market → Hub → Restaurant.
- The system shall support direct delivery routes (Market → Restaurant).
- The system shall optimize routes based on distance, time, and cost.
- The system shall assign vehicles and manage delivery schedules.
- The system shall support multi-stop delivery (multi-drop routing).

---

## **4. Hub Management System**

- The system shall manage goods aggregation at hubs.
- The system shall support cross-docking and goods exchange between routes.
- The system shall track incoming and outgoing goods at hubs.
- The system shall suggest optimal redistribution strategies.

---

## **5. Demand & Supply Matching (Advanced)**

- The system shall aggregate orders from multiple restaurants.
- The system shall suggest optimized batching strategies to reduce delivery cost.
- The system shall recommend delay or grouping options to users for cost efficiency.

---

## **6. AI Shopping Assistant (Advanced)**

- The system shall provide a natural-language chat interface where restaurant users can search products, check real-time price and stock, build and edit a draft order, and reorder from history.
- The AI shall perform intent recognition and conversation only, orchestrating existing backend commands/queries; it shall never access the database directly and shall never fabricate product or price data (every reference grounded in a real query result).
- The AI shall never auto-confirm an order: a hard, non-AI confirmation step is required before placement, and all server-side business rules (credit limit, cutoff/delivery window, ownership) continue to apply.
- The assistant shall run under the authenticated user's own identity (JWT) and inherit the same role-based access control.

---

## **7. Dashboard & Analytics**

- The system shall provide dashboards showing:
    - Price trends
    - Demand distribution
    - Delivery performance
- The system shall visualize supply-demand heatmaps.

---

## **8. Authentication & Notification System**

- The system shall support user authentication and RBAC across roles: Admin, Market Agent, Restaurant, Hub Staff, and Driver (Operations Manager is merged into Admin; "Kiosk Staff" is a legacy alias of Market Agent).
- The system shall send real-time notifications for:
    - Price updates
    - Order status
    - Delivery updates

---

# **Non-functional Requirements**

---

## **1. Performance**

- The system should provide real-time updates with minimal latency.
- The system should handle multiple concurrent users.
- The system should process logistics calculations efficiently.

---

## **2. Availability & Reliability**

- The system should ensure stable operation during peak hours.
- The system should handle failures gracefully.
- The system should ensure data consistency for orders and pricing.

---

## **3. Security**

- The system shall implement authentication and authorization using JWT.
- The system shall protect sensitive data (orders, pricing, user data).
- The system shall validate all user inputs.

---

## **4. Usability**

- The system should provide an intuitive interface for kiosk staff and restaurant users.
- The system should display pricing and ordering clearly.
- The system should provide easy-to-understand analytics dashboards.

---

## **5. Maintainability**

- The system should follow modular architecture.
- The system should support API documentation.
- The system should include logging and monitoring.

---

## **6. Deployment**

- The system should be deployable using Docker.
- The system should support cloud deployment.
- The system should support CI/CD integration.

---

# **(*) 3.2. Main proposal content**

---

## **Theory and practice (document)**

- Research on **Real-time Data Synchronization** using WebSocket/SignalR for live pricing systems.
- Research on **Supply Chain Optimization Models**, including routing and distribution strategies.
- Research on **Vehicle Routing Problem (VRP)** for logistics optimization.
- Research on **B2B Procurement Systems** and digital transformation in wholesale markets.
- Research on **LLM-based conversational agents and tool/function calling** for orchestrating backend operations from natural language (AI Shopping Assistant), including prompt-injection containment, response grounding, and human-in-the-loop confirmation.
- Research on **Hub-and-Spoke Distribution Models** in logistics systems.

---

# **Products**

---

## **Program**

### **FreshFlow Platform (For Market, Hub, Restaurant)**

- **Market Agent App:** (internal FreshFlow employees)
    - Real-time price and inventory updates
- **Restaurant App/Web:**
    - Order management
    - Real-time pricing dashboard
- **Logistics System:**
    - Route optimization
    - Vehicle scheduling
- **Hub Management System:**
    - Goods aggregation and redistribution
- **Analytics Dashboard:**
    - Price trends
    - Demand insights

---

## **Management Dashboard (Admin)**

- User management (market agent, restaurant, hub staff, driver, admin) + restaurant credit limits
- Pricing and data monitoring
- Logistics performance tracking
- System configuration

---

# **Proposed Tasks**

---

### **Task package 1: Product Catalog, Real-Time Pricing & Market Agent System**

- Develop the product catalog (products, categories, units, markets)
- Develop the Market Agent interface for price/quantity updates (incl. optional voice / speech-to-text input with read-back confirmation)
- Implement real-time synchronization using SignalR/WebSocket

---

### **Task package 2: Order Management System**

- Develop restaurant ordering system
- Implement scheduling and bulk order features
- Implement B2B credit / công nợ settlement (credit limits, charge on confirm, settlement, refunds)

---

### **Task package 3: Logistics Optimization Module**

- Implement route planning algorithms
- Develop vehicle assignment and scheduling logic

---

### **Task package 4: Hub Management Module**

- Implement goods aggregation and redistribution logic
- Develop cross-docking functionality

---

### **Task package 5: AI Shopping Assistant & Analytics Module (Optional Advanced)**

- Implement the AI Shopping Assistant (natural-language chat → host-layer orchestration over existing commands/queries; grounding + hard human-gated order confirmation)
- Develop analytics dashboards

---

### **Task package 6: Integration, Deployment, and Testing**

- Integrate all modules
- Perform system testing (UAT)
- Deploy using Docker and cloud services
- Complete final report