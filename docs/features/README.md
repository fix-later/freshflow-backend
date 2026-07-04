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

## logistics — Logistics module

| Doc | Loại |
|---|---|
| [CONTEXT-2026-06-21 requirement analysis](./logistics/CONTEXT-2026-06-21-logistics-requirement-analysis.md) | Context |
| [CONTEXT-2026-06-21 standalone demo](./logistics/CONTEXT-2026-06-21-standalone-logistics-demo.md) | Context |

## auth-catalog — Auth / Profile / Catalog

| Doc | Loại |
|---|---|
| [AUDIT-2026-06-10 auth-profile-catalog](./auth-catalog/AUDIT-2026-06-10-auth-profile-catalog.md) | Audit |

---

**Quy ước đặt tên:** `<TYPE>-<YYYY-MM-DD>-<slug>.md` — TYPE ∈ {SURVEY, AUDIT, CONTEXT, DESIGN, TASKS, BACKLOG}. Doc mới của một feature đặt vào đúng thư mục con của nó.
