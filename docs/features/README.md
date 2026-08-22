# Feature Working Docs

Tài liệu **theo từng feature** (survey, audit, context, plan, design, tasks, backlog) — tách khỏi
bộ spec core đánh số ở `docs/` để dễ tracking. Mỗi thư mục con = 1 feature / 1 đợt phân tích.

> Bộ spec core (`00-index`, `01`–`07`, `enums`, `REVIEW-REPORT`) nằm ở `docs/` gốc.
> Diagram ở `docs/diagrams/`, DBML/ERD ở `docs/database/`.
>
> **Trạng thái của từng đầu việc nằm trong chính tài liệu đó**, không nhân bản ở đây — bảng này
> chỉ là mục lục. Jira thường trễ so với code; khi cần biết một task đã xong chưa, đọc code
> hoặc git log, đừng tin trạng thái Jira.

Index này được sinh lại từ nội dung thư mục ngày **2026-08-22**.

## admin — Admin & System Config (epic SCRUM-353…361)

| Doc | Loại |
|---|---|
| [ADM Epic — Admin & System Configuration (Backend Plan / Audit)](./admin/AUDIT-2026-07-13-adm-backend-plan.md) | Audit / Plan |

## ai-assistant — AI Shopping Assistant (host layer, không phải module)

| Doc | Loại |
|---|---|
| [Backlog định nghĩa Epic / Task — AI Assistant Thin Queries](./ai-assistant/BACKLOG-2026-06-20-ai-assistant-thin-queries.md) | Backlog |
| [Design: AI Shopping Assistant — Tầng 2 (Orchestration Layer)](./ai-assistant/DESIGN-2026-06-22-ai-assistant-tier2-orchestration.md) | Design |
| [Plan: Mở rộng 6 tool P0 cho AI Assistant](./ai-assistant/PLAN-2026-08-11-assistant-p0-tools.md) | Plan |
| [Plan: Streaming cho AI Assistant](./ai-assistant/PLAN-2026-08-11-assistant-streaming.md) | Plan |
| [Survey: AI Shopping Assistant — Feasibility & Landscape](./ai-assistant/SURVEY-2026-06-18-ai-shopping-assistant-feasibility.md) | Survey |
| [Task Breakdown — AI Assistant Thin Queries (3 gaps)](./ai-assistant/TASKS-2026-06-20-ai-assistant-thin-queries.md) | Tasks |
| [Tasks: AI Shopping Assistant — Tầng 2 (Orchestration) — MVP v1](./ai-assistant/TASKS-2026-06-22-ai-assistant-tier2-orchestration.md) | Tasks |
| [jira-import-tier2-2026-06-22.csv](./ai-assistant/jira-import-tier2-2026-06-22.csv) | Jira import |

## analytics — Analytics Dashboard (epic SCRUM-301) — module read-only

| Doc | Loại |
|---|---|
| [Báo cáo khảo sát & kế hoạch triển khai: ANA — Operations Dashboard & Analytics](./analytics/AUDIT-2026-07-16-ana-backend-plan.md) | Audit / Plan |

## auth-catalog — Auth, Profile & Catalog

| Doc | Loại |
|---|---|
| [Báo cáo rà soát: Auth · Account/Profile · Catalog & Market](./auth-catalog/AUDIT-2026-06-10-auth-profile-catalog.md) | Audit / Plan |
| [Audit / Plan — Category cha/con](./auth-catalog/AUDIT-2026-07-21-category-hierarchy-plan.md) | Audit / Plan |

## credit — B2B Credit / Công nợ (epic SCRUM-254, nằm trong module Orders)

| Doc | Loại |
|---|---|
| [Báo cáo khảo sát & kế hoạch triển khai: CRE — B2B Credit, Debt & Statement](./credit/AUDIT-2026-07-07-cre-backend-plan.md) | Audit / Plan |
| [Dev Plan: Settlement, Credit Statement PDF, and Invoice Stub Hardening](./credit/PLAN-2026-08-10-dev-settlement-statement-invoice-stub.md) | Plan |

## delivery — Delivery Execution — last-mile (epic SCRUM-315…325, module Logistics)

| Doc | Loại |
|---|---|
| [Báo cáo khảo sát & kế hoạch triển khai: DEL — Delivery Execution (driver last-mile)](./delivery/AUDIT-2026-07-11-del-backend-plan.md) | Audit / Plan |

## hub — Hub Operations (epic SCRUM-256)

| Doc | Loại |
|---|---|
| [Báo cáo khảo sát & kế hoạch triển khai: HUB — Hub Operations, Reconciliation & Cross-docking](./hub/AUDIT-2026-07-10-hub-backend-plan.md) | Audit / Plan |
| [Khảo sát & kế hoạch triển khai: Hub Staff Assignment](./hub/AUDIT-2026-07-21-hub-staff-assignment-plan.md) | Audit / Plan |
| [Khảo sát & kế hoạch triển khai: Market–Hub Mapping và Daily Procurement Plan cho Hub](./hub/AUDIT-2026-07-26-market-hub-procurement-plan.md) | Audit / Plan |
| [Khảo sát & kế hoạch triển khai: Hub Sorting Detail, Loading Manifest linkage & Driver Handover (FE P1 mục 10–14)](./hub/AUDIT-2026-07-28-hub-sorting-driver-handover-plan.md) | Audit / Plan |
| [AUDIT / PLAN — Route Suggestions (gợi ý chợ + nhà hàng theo ngày)](./hub/AUDIT-2026-07-28-route-suggestions-plan.md) | Audit / Plan |
| [order-route-flow.drawio](./hub/order-route-flow.drawio) | Diagram |

## invoicing — Hóa đơn VAT (epic SCRUM-370…378) — module Invoicing

| Doc | Loại |
|---|---|
| [PLAN — VAT e-Invoicing (Hóa đơn điện tử)](./invoicing/PLAN-2026-07-25-invoicing-vat.md) | Plan |

## logistics — Logistics — route planning, Goong, vehicle

| Doc | Loại |
|---|---|
| [Báo cáo khảo sát & kế hoạch triển khai: LOG — Route Planning & Optimization](./logistics/AUDIT-2026-07-09-log-backend-plan.md) | Audit / Plan |
| [Context phan tich requirement: Logistics / Van chuyen](./logistics/CONTEXT-2026-06-21-logistics-requirement-analysis.md) | Context |
| [Context dự án demo Logistics độc lập](./logistics/CONTEXT-2026-06-21-standalone-logistics-demo.md) | Context |
| [PLAN (ACTIVE) — Goong Road Distance + Delivery Fee Snapshot](./logistics/PLAN-2026-08-06-goong-delivery-fee.md) | Plan |
| [PLAN (DEPRECATED) — OSRM Road Routing + OR-Tools VRP](./logistics/PLAN-2026-08-06-osrm-ortools-DEPRECATED.md) | Plan — ⛔ DEPRECATED |
| [Goong + OR-Tools multi-vehicle route planning](./logistics/PLAN-2026-08-09-goong-ortools-route-planning.md) | Plan |
| [PLAN (ACTIVE) — Cache Goong Distance Matrix (pairwise, durable)](./logistics/PLAN-2026-08-11-goong-matrix-cache.md) | Plan |
| [PLAN — Gắn xe (Vehicle) vào Hub, ranh giới cứng khi dispatch](./logistics/PLAN-2026-08-12-vehicle-hub-assignment.md) | Plan |

## notifications — Notifications (epic SCRUM-300)

| Doc | Loại |
|---|---|
| [Báo cáo khảo sát & kế hoạch triển khai: NOT — Notifications & Alerts (Backend)](./notifications/AUDIT-2026-07-08-not-backend-plan.md) | Audit / Plan |
| [PLAN (IMPLEMENTED) — Expo Push Notifications + SignalR Notification Realtime](./notifications/PLAN-2026-08-08-expo-push-signalr.md) | Plan |

## orders — Orders module

| Doc | Loại |
|---|---|
| [Báo cáo nghiên cứu & kế hoạch triển khai: Orders Module](./orders/AUDIT-2026-06-17-orders-module-research.md) | Audit / Plan |
| [AUDIT / PLAN — Restaurant Favorites (server-backed wishlist)](./orders/AUDIT-2026-07-23-favorites-backend-plan.md) | Audit / Plan |
| [PLAN — Lịch định kỳ chọn sản phẩm + tự động đặt đơn (bỏ draft rỗng)](./orders/PLAN-2026-08-07-scheduled-order-autoconfirm.md) | Plan |
| [order-flow-diagrams.drawio](./orders/diagrams/order-flow-diagrams.drawio) | Diagram |
| [Order flow review](./orders/order-flow-analysis.md) | Analysis |

## pricing — Pricing — bảng giá chợ, tags

| Doc | Loại |
|---|---|
| [PLAN — Bỏ số lượng sản phẩm trong chợ → cờ `IsAvailable`](./pricing/PLAN-2026-08-07-drop-market-quantity.md) | Plan |
| [PLAN — Market Product Tags (replace `is_featured`)](./pricing/PLAN-2026-08-07-market-product-tags.md) | Plan |
| [PLAN — Market Product Tags as a managed table (supersedes `text[]`)](./pricing/PLAN-2026-08-08-market-product-tags-table.md) | Plan |

## procurement — Procurement — phiên chợ / ProcurementBatch, market sessions

| Doc | Loại |
|---|---|
| [SCRUM-271 — Build Procurement Batch — Implementation Plan](./procurement/AUDIT-2026-07-14-proc-271-plan.md) | Audit / Plan |
| [SCRUM-272 — [UC-PROC-05][BE] Generate Procurement Manifest — Implementation Plan](./procurement/AUDIT-2026-07-14-proc-272-plan.md) | Audit / Plan |
| [SCRUM-274 — [UC-PROC-06][BE] Assign Market Agent — Implementation Plan](./procurement/AUDIT-2026-07-15-proc-274-plan.md) | Audit / Plan |
| [SCRUM-276 — [UC-PROC-07][BE] View Procurement Task & Manifest — Implementation Plan](./procurement/AUDIT-2026-07-15-proc-276-plan.md) | Audit / Plan |
| [SCRUM-278 — [UC-PROC-08][BE] Confirm Purchased Items — Implementation Plan](./procurement/AUDIT-2026-07-15-proc-278-plan.md) | Audit / Plan |
| [SCRUM-280 — [UC-PROC-09+10][BE] Report Procurement Exception & Proof — PLAN (plan only)](./procurement/AUDIT-2026-07-15-proc-280-plan.md) | Audit / Plan |
| [SCRUM-282 — [UC-PROC-11][BE] Handover Goods to Hub — Implementation Plan](./procurement/AUDIT-2026-07-15-proc-282-plan.md) | Audit / Plan |
| [SCRUM-284 — [UC-PROC-?][BE] Monitor Procurement Progress — Implementation Plan](./procurement/AUDIT-2026-07-15-proc-284-plan.md) | Audit / Plan |
| [PLAN — ProcurementBatch "Completed" status](./procurement/PLAN-2026-08-06-batch-completed-status.md) | Plan |
| [PLAN — Nhiều market agent / một phiên chợ (chia danh sách hàng)](./procurement/PLAN-2026-08-12-multi-agent-batch.md) | Plan |
| [PLAN — Phiên chợ 360 (Batch Overview read-model)](./procurement/PLAN-2026-08-13-batch-360-overview.md) | Plan |
| [PLAN — Phiên chợ độc lập theo market, tự sinh rolling 7 ngày](./procurement/PLAN-2026-08-13-market-sessions.md) | Plan |

## Tài liệu chung

| Doc | Loại |
|---|---|
| [ROADMAP-2026-07-31 — khoảng cách so với Kamereo](./ROADMAP-2026-07-31-kamereo-gap.md) | Roadmap |
