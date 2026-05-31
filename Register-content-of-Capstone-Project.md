## **(*) 3.1. Capstone Project name**

### **English:**

FreshFlow – An Intermediary Platform for Food Procurement and Logistics Optimization from Wholesale Markets for Restaurants in Ho Chi Minh City

### **Vietnamese:**

FreshFlow – Nền tảng trung gian thu mua và tối ưu vận chuyển thực phẩm từ chợ đầu mối cho các nhà hàng tại TPHCM

### **Abbreviation:**

**FFX**

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

- **Market Kiosk System:**
Enable vendors or staff at wholesale markets to update product prices and quantities in real time.
- **Restaurant Procurement Platform:**
Allow restaurants to view real-time prices, place bulk orders, and schedule recurring purchases.
- **Hub-based Distribution System:**
Aggregate goods from markets, support cross-docking, and redistribute to restaurants efficiently.
- **Logistics Optimization Engine:**
Optimize routes, schedules, and vehicle allocation to reduce delivery costs.
- **AI-based Price Prediction (Optional Advanced):**
Forecast price trends to support better procurement decisions.

---

# **Functional Requirements**

---

## **1. Real-Time Pricing Management**

- The system shall allow kiosk users to update product prices and available quantities.
- The system shall synchronize price updates in real time to all connected clients.
- The system shall maintain historical pricing data for analysis.
- The system shall notify users when significant price changes occur.

---

## **2. Order Management System**

- The system shall allow restaurants to create and manage bulk orders.
- The system shall support scheduled orders (daily/weekly).
- The system shall allow users to track order status in real time.
- The system shall support order grouping for logistics optimization.

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

## **6. AI Price Prediction (Advanced)**

- The system shall analyze historical price data to predict future trends.
- The system shall provide recommendations such as:
    - “Buy now” or “Wait for price drop”
- The system shall visualize price trends for users.

---

## **7. Dashboard & Analytics**

- The system shall provide dashboards showing:
    - Price trends
    - Demand distribution
    - Delivery performance
- The system shall visualize supply-demand heatmaps.

---

## **8. Authentication & Notification System**

- The system shall support user authentication (Admin, Kiosk Staff, Restaurant).
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
- Research on **AI-based Price Prediction Models** using historical data.
- Research on **Hub-and-Spoke Distribution Models** in logistics systems.

---

# **Products**

---

## **Program**

### **FreshFlow Platform (For Market, Hub, Restaurant)**

- **Market Kiosk App:**
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

- User management (kiosk, restaurant, admin)
- Pricing and data monitoring
- Logistics performance tracking
- System configuration

---

# **Proposed Tasks**

---

### **Task package 1: Real-Time Pricing & Kiosk System**

- Develop kiosk interface for price updates
- Implement real-time synchronization using SignalR/WebSocket

---

### **Task package 2: Order Management System**

- Develop restaurant ordering system
- Implement scheduling and bulk order features

---

### **Task package 3: Logistics Optimization Module**

- Implement route planning algorithms
- Develop vehicle assignment and scheduling logic

---

### **Task package 4: Hub Management Module**

- Implement goods aggregation and redistribution logic
- Develop cross-docking functionality

---

### **Task package 5: AI & Analytics Module (Optional Advanced)**

- Implement price prediction model
- Develop analytics dashboards

---

### **Task package 6: Integration, Deployment, and Testing**

- Integrate all modules
- Perform system testing (UAT)
- Deploy using Docker and cloud services
- Complete final report