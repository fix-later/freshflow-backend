# Feature Working Docs

Tài liệu **theo từng feature** (survey, audit, context, design, tasks, backlog) — tách khỏi bộ spec core đánh số ở `docs/` để dễ tracking. Mỗi thư mục con = 1 feature/đợt phân tích.

> Bộ spec core ổn định (`00-index`, `01`-`06`, `REVIEW-REPORT`, schema, enums) vẫn nằm ở `docs/` gốc. Diagram và DBML companion docs nằm trong `docs/diagrams/` và `docs/database/`.

## ai-assistant — AI Shopping Assistant

| Doc | Loại | Trạng thái |
|---|---|---|
| [SURVEY-2026-06-18 feasibility](./ai-assistant/SURVEY-2026-06-18-ai-shopping-assistant-feasibility.md) | Survey | ✅ |
| [BACKLOG-2026-06-20 thin queries](./ai-assistant/BACKLOG-2026-06-20-ai-assistant-thin-queries.md) | Backlog | ✅ |
| [TASKS-2026-06-20 thin queries (Tầng 1)](./ai-assistant/TASKS-2026-06-20-ai-assistant-thin-queries.md) | Tasks | ✅ DONE (SCRUM-235…246) |
| [DESIGN-2026-06-22 tier-2 orchestration](./ai-assistant/DESIGN-2026-06-22-ai-assistant-tier2-orchestration.md) | Design | 📐 Chờ duyệt |
| [TASKS-2026-06-22 tier-2 (Tầng 2)](./ai-assistant/TASKS-2026-06-22-ai-assistant-tier2-orchestration.md) | Tasks | ⏳ Chờ SCRUM key |
| [jira-import-tier2-2026-06-22.csv](./ai-assistant/jira-import-tier2-2026-06-22.csv) | Jira import | ⏳ |

## orders — Order module

| Doc | Loại |
|---|---|
| [AUDIT-2026-06-17 orders module research](./orders/AUDIT-2026-06-17-orders-module-research.md) | Audit |
| [order-flow-analysis](./orders/order-flow-analysis.md) | Analysis |
| [order-flow-diagrams.drawio](./orders/diagrams/order-flow-diagrams.drawio) | Diagram |

## credit — B2B Credit, Debt & Statement (epic SCRUM-254, trong module Orders)

| Doc | Loại | Trạng thái |
|---|---|---|
| [AUDIT-2026-07-07 cre backend plan](./credit/AUDIT-2026-07-07-cre-backend-plan.md) | Audit / Plan | 🟡 SCRUM-257 đang làm |

## notifications — Notifications & Alerts (epic SCRUM-300, module Notifications)

| Doc | Loại | Trạng thái |
|---|---|---|
| [AUDIT-2026-07-08 not backend plan](./notifications/AUDIT-2026-07-08-not-backend-plan.md) | Audit / Plan | ✅ Hoàn thành (SCRUM-327/330/331/334) |

## logistics — Logistics module (epic SCRUM-298 — Route Planning & Optimization)

| Doc | Loại | Trạng thái |
|---|---|---|
| [AUDIT-2026-07-09 log backend plan](./logistics/AUDIT-2026-07-09-log-backend-plan.md) | Audit / Plan | ⬜ 7 task chưa bắt đầu |
| [CONTEXT-2026-06-21 requirement analysis](./logistics/CONTEXT-2026-06-21-logistics-requirement-analysis.md) | Context | — |
| [CONTEXT-2026-06-21 standalone demo](./logistics/CONTEXT-2026-06-21-standalone-logistics-demo.md) | Context | — |

## hub — Hub Operations, Reconciliation & Cross-docking (epic SCRUM-256, module Hub)

| Doc | Loại | Trạng thái |
|---|---|---|
| [AUDIT-2026-07-10 hub backend plan](./hub/AUDIT-2026-07-10-hub-backend-plan.md) | Audit / Plan | ✅ Hoàn thành 6/6 (286/288/290/292/294 committed+reviewed, 296 đóng Option A), merged PR #22 |

## delivery — Delivery Execution (epic DEL, module Logistics — driver last-mile)

| Doc | Loại | Trạng thái |
|---|---|---|
| [AUDIT-2026-07-11 del backend plan](./delivery/AUDIT-2026-07-11-del-backend-plan.md) | Audit / Plan | 🟡 6 BE keys 315/317/319/321/323/325; 315+317+321 committed, 319-start CODED chờ bundle với remainder |

## auth-catalog — Auth / Profile / Catalog

| Doc | Loại |
|---|---|
| [AUDIT-2026-06-10 auth-profile-catalog](./auth-catalog/AUDIT-2026-06-10-auth-profile-catalog.md) | Audit |

---

**Quy ước đặt tên:** `<TYPE>-<YYYY-MM-DD>-<slug>.md` — TYPE ∈ {SURVEY, AUDIT, CONTEXT, DESIGN, TASKS, BACKLOG}. Doc mới của một feature đặt vào đúng thư mục con của nó.
