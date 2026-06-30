# Tasks: AI Shopping Assistant — Tầng 2 (Orchestration) — MVP v1

| | |
|---|---|
| **Date** | 2026-06-22 |
| **Design** | [`DESIGN-2026-06-22-ai-assistant-tier2-orchestration.md`](./DESIGN-2026-06-22-ai-assistant-tier2-orchestration.md) |
| **Branch** | `SCRUM-234-ai-shopping-assistant-query-foundations` (hoặc branch mới khi có epic key) |
| **Scope v1** | **MVP 5 tool**, **non-streaming**, provider **GLM 5.2 (Free) / ZenMux**, store **DB (Postgres)** — Redis hoãn |
| **Depends on** | Tầng 1 (3 thin query) — ✅ DONE (SCRUM-235…246) |
| **Status** | ✅ **T1–T7 CODE XONG trên working tree** (1280/1280 test GREEN, format gate PASS) — ⏳ Chờ SCRUM key để commit. **KHÔNG commit khi chưa có Jira key của supervisor** |

> **Tiến độ (2026-06-22):** T1 (LLM client) ✅ · T2 (tool registry) ✅ · T3 (DB store + migration `AddAssistantConversations`) ✅ · T4 (ConfirmationGate, 10 test) ✅ · T5 (orchestrator + `POST /api/v1/assistant/chat` + DTO + system prompt + wiring, 5 test) ✅ · T6 (rate-limit policy `assistant` + config + fail-fast validation) ✅ · T7 (format gate PASS, full suite 1280 GREEN, không regression Tầng 1) ✅. Assistant unit tests: **57**. Còn lại (tùy chọn, có thể đưa vào T7+ hoặc giai đoạn sau): integration test HTTP `POST /chat` qua `WebApplicationFactory` với fake `IAssistantChatClient`, và test 429 cho policy `assistant`.

> Code prefix tạm: `ASSIST2-*` (đổi sang SCRUM key thật khi supervisor cấp). Commit format: `type(scope): SCRUM-XXX description`. Format gate qua `FreshFlow.slnx`.

---

## Definition of Done (toàn epic)

- [ ] `POST /api/v1/assistant/chat` chạy được end-to-end: user nhắn → AI search → tạo draft → preview → confirm (có cờ) → trả kết quả.
- [ ] LLM nằm sau `IAssistantChatClient`; `ZenMuxChatClient` là impl duy nhất; **không** rò rỉ provider ra orchestrator.
- [ ] **Safety gate two-phase**: `confirm_order` không bao giờ chạy nếu thiếu cờ tường minh từ client (test chứng minh).
- [ ] `ApiKey` nạp từ ENV (`Assistant__ZenMux__ApiKey`), **không hardcode**; fail-fast nếu thiếu lúc startup.
- [ ] **Không** gửi PII / số công nợ thật vào prompt (lọc ở tool-result mapper).
- [ ] Rate-limit policy `assistant` gắn trên controller.
- [ ] Unit + integration test (fake LLM client, **không** gọi GLM thật trong CI); coverage ≥ 80% phần orchestrator/registry/gate.
- [ ] `dotnet format FreshFlow.slnx --verify-no-changes` PASS; toàn bộ test GREEN.
- [ ] Regression: không sửa nghiệp vụ `ConfirmOrderCommandHandler` / các thin query Tầng 1.

---

## MVP 5 tool (chốt phạm vi)

| Tool | MediatR target | LLM điền | Server inject |
|---|---|---|---|
| `search_products` | `SearchMarketProductsQuery` | searchText, category?, inStockOnly?, cursor? | MarketId |
| `create_draft_order` | `CreateDraftOrderCommand` | items[] (marketProductId, qty), scheduledFor?, notes? | UserId |
| `get_order` | `GetOrderQuery` | orderId | UserId, canReadAll=false |
| `preview_confirmation` | `PreviewOrderConfirmationQuery` | orderId | UserId |
| `confirm_order` | `ConfirmOrderCommand` | orderId | UserId — **chỉ sau ConfirmationGate** |

> add/update/remove item, list_orders, reorder_from_history → **giai đoạn sau** (DESIGN §11a).

---

## Tasks

### T1 — LLM client abstraction + ZenMux impl
**Folder:** `src/FreshFlow.API/Assistant/Abstractions/`, `Assistant/Llm/`
**Mô tả:** Định nghĩa `IAssistantChatClient` provider-agnostic; impl `ZenMuxChatClient` qua `Microsoft.Extensions.AI` (`IChatClient`) trỏ base_url ZenMux + model `z-ai/glm-5.2-free`. `ZenMuxOptions` (BaseUrl, Model, ApiKey, TimeoutSeconds, MaxToolHops).
**Acceptance:**
- `CompleteAsync(state, tools, ct)` trả `AssistantTurnResult` (text **hoặc** tool_call(name, args)).
- ApiKey đọc từ config binding `Assistant:ZenMux` (giá trị từ ENV); options validate fail-fast khi rỗng.
- Timeout + retry/backoff (Polly 2 lần) cho 5xx/timeout/429.
**Tests:** unit cho options validation; mapping request/response với `IChatClient` mock.
**Deps:** none. **Effort:** M.
**Commit:** `feat(assistant): SCRUM-XXX add IAssistantChatClient + ZenMux GLM client`

### T2 — Tool registry + 5 MVP tool definitions
**Folder:** `Assistant/Tools/`
**Mô tả:** `AssistantTool` (Name, Description, JsonSchema, Handler); `AssistantToolRegistry.InvokeAsync(name, jsonArgs, ctx)` validate args → dựng record (inject UserId từ JWT, MarketId từ ctx) → `ISender.Send()` → map `Result<T>` về JSON. `ToolDefinitions` khai báo schema 5 tool MVP.
**Acceptance:**
- LLM **không** điền được UserId/MarketId (server inject; bỏ qua nếu LLM cố gửi).
- `Result.Failure` → `{ error: Code, message }` trả về LLM (không ném exception).
- Tool-result mapper **loại** field nhạy cảm khỏi payload gửi LLM (vd `RemainingCreditAfter` không vào prompt).
- Tool lạ / args sai schema → lỗi có cấu trúc để LLM tự sửa.
**Tests:** unit dispatch đúng record + inject; map success & failure; lọc field nhạy cảm.
**Deps:** T1 (kiểu chung). **Effort:** M.
**Commit:** `feat(assistant): SCRUM-XXX add tool registry + 5 MVP tool definitions`

### T3 — Conversation store (DB / Postgres — Redis hoãn)
**Folder:** `Assistant/Conversation/` (store + state) + `FreshFlow.Infrastructure.Persistence/` (entity + EF config + migration)
**Mô tả:** `IConversationStore` + `DbConversationStore` (EF Core trên shared `AppDbContext`). `ConversationState` (SessionId, Turns[], CurrentDraftOrderId?, MarketId, UserId). Entity `AssistantConversation` (id, session_id unique, user_id, market_id?, `state JSONB`, created_at, updated_at, expires_at) + `IEntityTypeConfiguration` trong Persistence; **migration** `dotnet ef migrations add AddAssistantConversations` vào Persistence. Lazy-expire khi đọc (`expires_at > now`). **Hard-delete**, không `deleted_at`. Cắt history theo token-window.
**Acceptance:**
- Load/append/save round-trip qua Postgres; `expires_at` set +30' mỗi lần ghi; row hết hạn coi như không có khi đọc.
- `state` lưu dạng `jsonb` (`.HasColumnType("jsonb")`).
- History vượt ngưỡng → giữ N turn gần nhất + system prompt.
- Migration apply sạch; entity auto-discover qua `ApplyConfigurationsFromAssembly`.
**Tests:** unit serialize/trim/lazy-expire (mock store); integration round-trip qua `AppDbContext` (in-memory hoặc test DB).
**Deps:** none. **Effort:** S→M (thêm migration).
**Commit:** `feat(assistant): SCRUM-XXX add DB-backed conversation store + migration`
> **Note:** `RedisConversationStore` (sliding TTL, scale-out) = giai đoạn sau — drop-in thay `DbConversationStore` nhờ `IConversationStore`, không đụng orchestrator.

### T4 — Confirmation safety gate (two-phase)
**Folder:** `Assistant/Safety/`
**Mô tả:** `ConfirmationGate` chặn `confirm_order` trừ khi request mang cờ tường minh `confirmOrderId` (từ nút UI) **khớp** `orderId`. Nếu thiếu → orchestrator chạy `preview_confirmation` và trả `pendingConfirmation` thay vì confirm.
**Acceptance:**
- `confirm_order` không có cờ → KHÔNG gọi `ConfirmOrderCommand`; trả pendingConfirmation.
- `confirmOrderId != orderId` → chặn.
- Có cờ khớp → cho qua.
**Tests:** unit cả 3 nhánh (thiếu cờ / lệch / khớp); chứng minh `ISender` **không** nhận `ConfirmOrderCommand` khi bị chặn.
**Deps:** T2. **Effort:** S.
**Commit:** `feat(assistant): SCRUM-XXX add two-phase confirmation gate`

### T5 — Orchestrator loop + controller + DTOs + wiring
**Folder:** `Assistant/`, `Assistant/Dtos/`, `AssistantController.cs`, `DependencyInjection.cs`
**Mô tả:** `POST /api/v1/assistant/chat` (auth JWT → UserId; load state; append message). Orchestrator loop: gọi `IAssistantChatClient` → nếu tool_call (qua gate T4 nếu confirm) → `AssistantToolRegistry` → append result → lặp (≤ `MaxToolHops`) → text cuối. Lưu state. `AssistantChatRequest(sessionId, message, marketId, confirmOrderId?)`, `AssistantChatResponse(reply, sessionId, pendingConfirmation?, draftOrderId?)`. `AssistantSystemPrompt` (vai trò, ràng buộc, tiếng Việt). `AddAssistant(config)` chain vào `Program.cs`. **Non-streaming.**
**Acceptance:**
- Luồng end-to-end: search → create_draft → preview → confirm(cờ) → reply.
- `MaxToolHops` chặn vòng lặp vô hạn → trả thông báo an toàn.
- Lỗi LLM → câu trả lời lịch sự, **không** lộ chi tiết kỹ thuật.
**Tests:** integration với **fake** `IAssistantChatClient` (kịch bản tool-call scripted), **không** gọi GLM thật.
**Deps:** T1–T4. **Effort:** M.
**Commit:** `feat(assistant): SCRUM-XXX add orchestrator loop + chat endpoint`

### T6 — Rate limiting + startup config validation
**Folder:** `Assistant/DependencyInjection.cs`, `Program.cs`, `appsettings*.json`
**Mô tả:** Policy `assistant` (tái dùng pattern `RateLimiting:Auth/Orders`) gắn lên `AssistantController`. Section `Assistant` trong appsettings (ApiKey rỗng, nạp ENV). Validate config lúc startup.
**Acceptance:**
- Vượt `PermitLimit/WindowMinutes` → 429.
- Thiếu `ApiKey` lúc startup → fail-fast với message rõ.
**Tests:** unit options validation; integration 429 khi vượt ngưỡng.
**Deps:** T5. **Effort:** S.
**Commit:** `feat(assistant): SCRUM-XXX add assistant rate-limit + config validation`

### T7 — Test pass + coverage + docs
**Mô tả:** Hoàn thiện unit + integration; đảm bảo coverage ≥ 80% orchestrator/registry/gate; cập nhật DESIGN doc nếu lệch; format gate.
**Acceptance:** toàn bộ test GREEN; `dotnet format FreshFlow.slnx --verify-no-changes` PASS; coverage đạt; regression Tầng 1 nguyên vẹn.
**Deps:** T1–T6. **Effort:** M.
**Commit:** `test(assistant): SCRUM-XXX integration + coverage for tier-2 orchestrator`

### T8 — (Optional) SCRUM-239 index product name cho ILIKE
**Mô tả:** EF migration thêm index hỗ trợ `products.name ILIKE` của `SearchMarketProductsQuery`.
**Acceptance:** migration add + apply sạch; search dùng index.
**Deps:** none (độc lập). **Effort:** XS. **Có thể hoãn.**
**Commit:** `perf(pricing): SCRUM-239 add product name index for ILIKE search`

---

## Thứ tự thực thi (critical path)

```
T1 ─┬─► T2 ─► T4 ─┐
    └─► T3 ────────┴─► T5 ─► T6 ─► T7
T8 (độc lập, bất kỳ lúc nào / hoãn)
```

## Commit-map (điền SCRUM key khi supervisor cấp)

| Task | Type/scope | SCRUM key |
|---|---|---|
| T1 | feat(assistant) | SCRUM-247 |
| T2 | feat(assistant) | SCRUM-248 |
| T3 | feat(assistant) | SCRUM-249 |
| T4 | feat(assistant) | SCRUM-250 |
| T5 | feat(assistant) | SCRUM-251 |
| T6 | feat(assistant) | SCRUM-252 |
| T7 | test(assistant) | SCRUM-253 |
| T8 | perf(pricing) | SCRUM-239 (optional) |

> **T7 (SCRUM-253) integration tests DONE (2026-06-23):** `tests/Integration/FreshFlow.IntegrationTests/Assistant/` — `AssistantChatEndpointTests` (3: 401 chưa auth, text-reply 200, confirm-bị-chặn → pendingConfirmation) + `AssistantRateLimitTests` (429 khi vượt policy `assistant`). Dùng `AssistantWebAppFactory` + `ScriptedAssistantChatClient` (fake LLM, không gọi GLM). 4/4 GREEN qua Testcontainers Postgres.

---

## Out-of-scope v1 (DESIGN §11a)

- **SSE streaming** (ưu tiên cao nhất nhóm "sau" — UX). `IAssistantChatClient` sẽ thêm `CompleteStreamingAsync`.
- **Redis conversation store** (đổi từ DB sang khi cần scale; drop-in nhờ `IConversationStore`).
- Cleanup job xóa conversation hết hạn (DB-store dùng lazy-expire trước).
- Full 10 tool (add/update/remove item, list_orders, reorder_from_history).
- Provider dự phòng khi hết quota free.
