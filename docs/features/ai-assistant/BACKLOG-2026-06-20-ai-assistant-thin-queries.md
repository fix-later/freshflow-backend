# Backlog định nghĩa Epic / Task — AI Assistant Thin Queries

| | |
|---|---|
| **Ngày lập** | 2026-06-20 |
| **Branch** | `SCRUM-179-ORD-Order-Management` |
| **Nguồn** | `docs/SURVEY-2026-06-18-ai-shopping-assistant-feasibility.md` §1a, §3, §7 |
| **Phạm vi** | 3 thin read-side query (module-side) làm tiền đề cho AI Shopping Assistant. **KHÔNG** gồm orchestrator/host/LLM (chờ design phase). |
| **Tác giả** | Claude Code, theo yêu cầu supervisor |
| **Trạng thái** | ✅ **Đã có Jira key (2026-06-20)** — Epic **SCRUM-234**, task con **SCRUM-235..246**. Commit per-task theo key con (KHÔNG dùng key epic). |

> **Mục đích:** Đây là file định nghĩa cấu trúc Epic → Task (chưa tồn tại trên Jira) để supervisor tạo issue và cấp SCRUM key. Chi tiết kỹ thuật/acceptance/test nằm ở file đồng hành **`docs/TASKS-2026-06-20-ai-assistant-thin-queries.md`**.

---

## 0. Quy ước mã nội bộ (placeholder cho tới khi có SCRUM key)

- **Tiền tố:** `ASSIST` = AI Assistant.
- **Đề xuất UC code** (đồng bộ phong cách UC-ORD hiện có): `UC-ASSIST-01/02/03`.
- **Mã task nội bộ:** `ASSIST-E{n}-T{m}`. Khi tạo Jira, map từng dòng sang một issue và điền **SCRUM Key**.
- **Commit format:** `type(scope): SCRUM-XXX description` — điền key sau khi cấp; trước đó để code ở working tree, **không commit**.

---

## 1. Epic registry

| Epic code | UC code | Epic title | Module | Effort | SCRUM Key | Trạng thái |
|---|---|---|---|---|---|---|
| ASSIST-E1 | UC-ASSIST-01 | Combined free-text product search (name + price + stock) | Pricing | **M** | SCRUM-234 (epic) → 235–239 | ✅ Đã tạo |
| ASSIST-E2 | UC-ASSIST-02 | Restaurant-facing market context / picker | Catalog | **XS** | SCRUM-234 (epic) → 240 | ✅ Đã tạo |
| ASSIST-E3 | UC-ASSIST-03 | Confirm-preview / dry-run order confirmation | Orders | **M** | SCRUM-234 (epic) → 241–246 | ✅ Đã tạo |

> Lưu ý: Jira thực tế là **1 epic SCRUM-234** + 12 task con; ASSIST-E1/E2/E3 là nhóm logic theo UC. Commit dùng **key task con** (235–246), không dùng key epic.

---

## 2. Task registry (con của Epic)

### Epic ASSIST-E1 — `SearchMarketProductsQuery` (Pricing)

| Task code | Title | Effort | Phụ thuộc | SCRUM Key |
|---|---|---|---|---|
| ASSIST-E1-T1 | DTOs: `MarketProductSearchItemDto` + `SearchMarketProductsResultDto` | S | — | SCRUM-235 |
| ASSIST-E1-T2 | Query record + FluentValidation `SearchMarketProductsQueryValidator` | S | T1 | SCRUM-236 |
| ASSIST-E1-T3 | `IMarketProductRepository.SearchAsync` + EF impl (Catalog join, ILIKE, cursor) | M | T1 | SCRUM-237 |
| ASSIST-E1-T4 | `internal` handler + unit tests | S | T2, T3 | SCRUM-238 |
| ASSIST-E1-T5 | (Optional) Index tên SP cho ILIKE — tách, không block | XS | T3 | SCRUM-239 |

### Epic ASSIST-E2 — Market context (Catalog)

| Task code | Title | Effort | Phụ thuộc | SCRUM Key |
|---|---|---|---|---|
| ASSIST-E2-T1 | Verify `GET /api/v1/markets` mở cho mọi authenticated user + document reuse `GetMarketsQuery` (zero new code) | XS | — | SCRUM-240 |

### Epic ASSIST-E3 — `PreviewOrderConfirmationQuery` (Orders)

| Task code | Title | Effort | Phụ thuộc | SCRUM Key |
|---|---|---|---|---|
| ASSIST-E3-T0 | Spike: chốt biểu diễn over-limit của `CanChargeAsync` (hiện = `Result.Failure`) | XS | — | SCRUM-241 |
| ASSIST-E3-T1 | Extract `OrderConfirmationEvaluator` (pure, non-mutating) + unit tests | M | T0 | SCRUM-242 |
| ASSIST-E3-T2 | Refactor `ConfirmOrderCommandHandler` dùng evaluator — **7 test cũ phải GREEN** | S | T1 | SCRUM-243 |
| ASSIST-E3-T3 | Preview DTOs: `OrderConfirmationPreviewDto` + `PreviewIssueDto` | S | — | SCRUM-244 |
| ASSIST-E3-T4 | Query + Validator + handler + tests (preview khớp confirm) | M | T1, T3 | SCRUM-245 |
| ASSIST-E3-T5 | Controller endpoint `GET /api/v1/orders/{orderId}/confirm-preview` | S | T4 | SCRUM-246 |

---

## 3. Thứ tự & song song

```
Luồng 1 (Pricing, ASSIST-E1):  T1 → T2/T3 → T4        [T5 optional, tách]
Luồng 2 (Catalog, ASSIST-E2):  T1 (XS, bất kỳ lúc nào)
Luồng 3 (Orders, ASSIST-E3):   T0 → T1 → T2 (gate: 7 tests GREEN) → T3/T4 → T5

3 epic khác module → chạy song song được. Gate cứng: ASSIST-E3-T2 không merge nếu test ConfirmOrder đổi/đỏ.
```

---

## 4. Checklist tạo Jira (cho supervisor)

- [ ] Tạo 3 Epic (ASSIST-E1/E2/E3) → điền cột **SCRUM Key** ở §1.
- [ ] Tạo task con theo §2 → điền **SCRUM Key** từng dòng.
- [ ] Quyết định UC code chính thức (đề xuất `UC-ASSIST-01..03`) hoặc map vào scheme Jira hiện hành.
- [ ] Sau khi có key: cập nhật `docs/TASKS-2026-06-20-ai-assistant-thin-queries.md` (§ commit map) rồi giao team `backend-dev`.

> **Lưu ý quy ước (memory):** không commit khi chưa có Jira key; mỗi UC/issue một key, không gộp nhiều key vào 1 commit; cập nhật cả file TASKS/AUDIT khi task hoàn thành, không chỉ TaskList.
