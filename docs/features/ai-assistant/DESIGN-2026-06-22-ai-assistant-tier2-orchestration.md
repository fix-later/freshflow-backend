# Design: AI Shopping Assistant — Tầng 2 (Orchestration Layer)

| | |
|---|---|
| **Date** | 2026-06-22 |
| **Branch** | `SCRUM-234-ai-shopping-assistant-query-foundations` |
| **Type** | Design proposal — **no code yet**, for review/approval before implementation |
| **Provider chosen** | **GLM 5.2 (Free)** via **ZenMux** gateway (OpenAI-compatible) |
| **Depends on** | Tầng 1 (3 thin queries) — ✅ DONE (epic SCRUM-234, 235–246) |
| **Premise** | Chat layer: user nhập ngôn ngữ tự nhiên → AI nhận diện ý định + hội thoại → orchestrate gọi **các MediatR command/query đã tồn tại**. AI **không** đụng DB trực tiếp, **không** auto-confirm đơn, mọi hành động đổi state đi qua Application layer. User phải xác nhận rõ ràng trước khi đặt đơn. |

> Doc này thiết kế **Tầng 2** — bộ não (LLM) + orchestrator + chat endpoint. Tầng 1 (tool surface = 3 thin query + các command có sẵn) đã hoàn tất. Mục tiêu phạm vi: **capstone/demo**, không phải production tải cao.

---

## 1. Quyết định kiến trúc (đã chốt từ survey, nhắc lại)

- **Option B — host-layer orchestration.** Toàn bộ Tầng 2 nằm trong `src/FreshFlow.API/Assistant/`. **KHÔNG** tạo module mới (không có `Assistant.Domain/Application/Infrastructure`). Lý do: assistant chỉ *điều phối* các module hiện có qua `ISender`, không sở hữu nghiệp vụ hay bảng DB nào.
- **Provider-agnostic.** LLM nằm sau interface `IAssistantChatClient`. GLM/ZenMux chỉ là **một** implementation → đổi sang Claude/GPT/model trả phí về sau = thay 1 class, không đụng orchestrator.
- **Tool = wrapper 1:1 trên MediatR.** Mỗi "tool" của LLM map đúng 1 command/query đã có. Tool layer **không chứa business rule** — chỉ map JSON args → record → `ISender.Send()` → map kết quả về JSON.

### Sơ đồ thư mục đề xuất

```
src/FreshFlow.API/Assistant/
  AssistantController.cs                 ← POST /api/v1/assistant/chat
  DependencyInjection.cs                 ← AddAssistant(config) — wiring + options
  Abstractions/
    IAssistantChatClient.cs              ← trừu tượng LLM (provider-agnostic)
    IAssistantToolRegistry.cs            ← danh mục tool + dispatch
    IConversationStore.cs                ← lưu/đọc lịch sử hội thoại
  Llm/
    ZenMuxChatClient.cs                  ← impl IAssistantChatClient (OpenAI-compatible)
    ZenMuxOptions.cs                     ← BaseUrl, Model, ApiKey, TimeoutSeconds...
  Tools/
    AssistantTool.cs                     ← record: Name, Description, JsonSchema, Handler
    AssistantToolRegistry.cs             ← đăng ký + dispatch tới ISender
    ToolDefinitions.cs                   ← khai báo schema của từng tool
  Conversation/
    DbConversationStore.cs               ← impl IConversationStore (EF Core + Postgres) — v1
    ConversationState.cs                 ← record: SessionId, Turns, CurrentDraftOrderId?
    (Redis impl = giai đoạn sau, xem §11a)
  Safety/
    ConfirmationGate.cs                  ← chặn confirm khi chưa có user "yes" rõ ràng
  Dtos/
    AssistantChatRequest.cs / AssistantChatResponse.cs
  AssistantSystemPrompt.cs               ← system prompt (vai trò, ràng buộc, ngôn ngữ VN)
```

---

## 2. Provider: GLM 5.2 (Free) qua ZenMux

| Thuộc tính | Giá trị | Nguồn |
|---|---|---|
| Base URL | `https://zenmux.ai/api/v1` | ZenMux docs |
| Model id | `z-ai/glm-5.2-free` | ZenMux models |
| Protocol | **OpenAI-compatible** (đổi base_url + model name) | ZenMux docs |
| Tool/function calling | ✅ Hỗ trợ (điều kiện sống còn của Tầng 2) | GLM model card |
| Gói | **Free, rate-limited** — đủ cho dev/demo, KHÔNG cho production | ZenMux subscription |

### .NET integration

OpenAI-compatible ⇒ dùng package **`Microsoft.Extensions.AI`** (+ `OpenAI` SDK) trỏ `Endpoint = BaseUrl`, `Model = z-ai/glm-5.2-free`. Không cần SDK riêng của Z.AI. `IAssistantChatClient` bọc `IChatClient` của `Microsoft.Extensions.AI` để giữ provider-agnostic.

```csharp
// ZenMuxChatClient (phác thảo — KHÔNG phải code cuối)
public interface IAssistantChatClient
{
    Task<AssistantTurnResult> CompleteAsync(
        ConversationState state,
        IReadOnlyList<AssistantTool> tools,
        CancellationToken ct);
}
```

### ⚠️ Ràng buộc dữ liệu (BẮT BUỘC tuân thủ)

Dữ liệu B2B (đơn hàng) + **công nợ/credit** là nhạy cảm và sẽ đi qua gateway bên thứ 3.
- **KHÔNG** đưa PII, số dư công nợ thật, mã định danh nội bộ vào prompt.
- Tool args/kết quả gửi cho LLM chỉ chứa: tên sản phẩm, số lượng, giá, `MarketId`, `OrderId` (UUID vô danh), mã issue (`CREDIT_LIMIT_EXCEEDED`...). `RemainingCreditAfter` **không** gửi cho LLM — chỉ trả thẳng cho client trong response payload, ngoài luồng prompt.
- Đọc kỹ điều khoản training-on-data của gói free tại `docs.zenmux.ai` trước khi cắm dữ liệu thật.

---

## 3. Tool Registry — map LLM tool → MediatR (Tầng 1)

Mỗi tool là wrapper mỏng. `UserId` và `MarketId` **không bao giờ** do LLM cung cấp — server inject từ JWT claim (UserId) và từ session context (MarketId client gửi kèm). LLM chỉ điền các tham số nghiệp vụ.

| Tool name (LLM) | MediatR target | Module | LLM được điền | Server inject |
|---|---|---|---|---|
| `search_products` | `SearchMarketProductsQuery` | Pricing | `searchText`, `category?`, `inStockOnly?`, `cursor?` | `MarketId` |
| `create_draft_order` | `CreateDraftOrderCommand` | Orders | `items[]` (marketProductId, qty), `scheduledFor?`, `notes?` | `UserId` |
| `add_item` | `AddOrderItemCommand` | Orders | `orderId`, `marketProductId`, `quantity` | `UserId` |
| `update_item_qty` | `UpdateOrderItemCommand` | Orders | `orderId`, `itemId`, `quantity` | `UserId` |
| `remove_item` | `RemoveOrderItemCommand` | Orders | `orderId`, `itemId` | `UserId` |
| `get_order` | `GetOrderQuery` | Orders | `orderId` | `UserId`, `canReadAll=false` |
| `list_orders` | `ListOrdersQuery` | Orders | filter (date/status) | `UserId` |
| `reorder_from_history` | `ReorderFromHistoryCommand` | Orders | `orderId`, `scheduledFor?`, `notes?` | `UserId` |
| `preview_confirmation` | `PreviewOrderConfirmationQuery` | Orders | `orderId` | `UserId` |
| **`confirm_order`** | `ConfirmOrderCommand` | Orders | `orderId` | `UserId` — **CHỈ sau ConfirmationGate** (§5) |

**Dispatch chung:** `AssistantToolRegistry.InvokeAsync(toolName, jsonArgs, ctx)` → validate args theo JSON schema → dựng record (inject UserId/MarketId) → `ISender.Send()` → `Result<T>` → map về JSON (success ⇒ data; failure ⇒ `{ error: Code, message }`). Lỗi nghiệp vụ trả về LLM dưới dạng có cấu trúc để AI diễn giải cho user (vd `CREDIT_LIMIT_EXCEEDED` → "Đơn này vượt hạn mức công nợ, bạn cần...").

---

## 4. Luồng hội thoại (sequence)

```
Client (đã chọn MarketId ở app-shell)
  │  POST /api/v1/assistant/chat { sessionId, message, marketId }
  ▼
AssistantController
  │  - auth JWT → UserId
  │  - load ConversationState(sessionId) từ Redis
  │  - append user message
  ▼
Orchestrator loop (IAssistantChatClient.CompleteAsync)
  │  1. gửi: system prompt + history + tool definitions → GLM 5.2
  │  2. GLM trả: text  HOẶC  tool_call(name, args)
  │  3. nếu tool_call:
  │       - confirm_order? → ConfirmationGate kiểm tra (§5)
  │       - AssistantToolRegistry.InvokeAsync → ISender.Send → kết quả JSON
  │       - append tool result vào history → quay lại bước 1
  │     (giới hạn N vòng lặp tool, vd MAX_TOOL_HOPS=6, chống vòng lặp vô hạn)
  │  4. nếu text → đó là câu trả lời cuối
  ▼
- lưu ConversationState về Redis
- trả AssistantChatResponse { reply, sessionId, pendingConfirmation?, draftOrderId? }
```

---

## 5. Safety Gate — chống auto-confirm (CỐT LÕI)

Đây là ràng buộc bất biến của premise: **user phải xác nhận rõ ràng trước khi đặt đơn.**

**Cơ chế 2 bước (two-phase), KHÔNG tin LLM tự kiềm chế:**

1. Khi LLM gọi `confirm_order`, `ConfirmationGate` **chặn lại** nếu turn hiện tại của user **chưa** chứa xác nhận rõ ràng cho *đúng* `orderId` đó. Thay vì confirm, orchestrator tự chạy `preview_confirmation` và trả về `pendingConfirmation` (tổng tiền, ngày giao, cảnh báo) + hỏi lại "Bạn xác nhận đặt đơn này?".
2. `confirm_order` **chỉ** được phép thực thi khi request mang cờ tường minh từ client — vd field `confirmOrderId` trong `AssistantChatRequest` do **nút bấm UI** set (không phải do model suy diễn từ text). Server so khớp `confirmOrderId == orderId` mới cho qua.

→ Ngay cả khi model "hallucinate" muốn confirm, gate vẫn chặn vì thiếu cờ tường minh từ UI. `ConfirmOrderCommand` (credit/cutoff/charge) vẫn là nguồn chân lý cuối — gate chỉ thêm 1 lớp chặn phía trước, không thay đổi nghiệp vụ.

---

## 6. Conversation State (DB-backed — v1; Redis hoãn)

**Quyết định (2026-06-22):** v1 lưu hội thoại trong **Postgres** (shared `AppDbContext`), **chưa** dùng Redis — tránh phải dựng thêm hạ tầng. `IConversationStore` giữ nguyên ⇒ đổi sang Redis sau là drop-in, orchestrator không đổi.

- **Bảng `assistant_conversations`** trong `FreshFlow.Infrastructure.Persistence`:
  - `id UUID PK` (`gen_random_uuid()`), `session_id` (unique, indexed — handle vô danh client gửi),
  - `user_id UUID`, `market_id UUID NULL`,
  - `state JSONB` — serialize `ConversationState` (`Turns[]`, `CurrentDraftOrderId?`),
  - `created_at`, `updated_at TIMESTAMPTZ`, `expires_at TIMESTAMPTZ`.
- **TTL:** cột `expires_at` (vd +30' mỗi lần ghi). **Lazy-expire** khi đọc (lọc `expires_at > now()` ⇒ coi như không có); cleanup job xóa hẳn để **giai đoạn sau** (chưa cần cho demo).
- **Soft-delete:** **KHÔNG** — conversation là dữ liệu tạm, **hard-delete** khi hết hạn (ngoại lệ giống `refresh_tokens`/`price_snapshots`, không có `deleted_at`).
- Cắt history theo token-window (giữ N turn gần nhất + system prompt) để khỏi vượt context của GLM.
- **Ghi chú Option B deviation:** Assistant host-layer giờ sở hữu đúng 1 bảng ephemeral. Entity + `IEntityTypeConfiguration` đặt trong Persistence project (auto-discover qua `ApplyConfigurationsFromAssembly`); migration theo lệnh chuẩn (`dotnet ef migrations add` vào Persistence). Chấp nhận được — không vi phạm dependency rule nào.

---

## 7. Config & Secrets (tuân thủ rule security)

```jsonc
// appsettings.json — KHÔNG để ApiKey thật ở đây
"Assistant": {
  "ZenMux": {
    "BaseUrl": "https://zenmux.ai/api/v1",
    "Model": "z-ai/glm-5.2-free",
    "ApiKey": "",            // ← nạp từ ENV: Assistant__ZenMux__ApiKey
    "TimeoutSeconds": 60,
    "MaxToolHops": 6
  },
  "RateLimiting": { "PermitLimit": 20, "WindowMinutes": 1 }
}
```

- `ApiKey` **chỉ** nạp từ biến môi trường / secret manager (`Assistant__ZenMux__ApiKey`), **không hardcode** (precedent: `Email.ResendApiKey`). Validate có mặt lúc startup, fail-fast nếu thiếu.
- Đăng ký 1 extension method `services.AddAssistant(config)` chain vào `Program.cs`, đúng pattern module hiện có.

---

## 8. Rate limiting (chống cháy token free-tier)

- Tái dùng pattern đã có (`RateLimiting:Auth/Orders` với `PermitLimit`/`WindowMinutes`). Thêm policy `assistant` gắn vào `AssistantController`.
- Lý do: gói free đã rate-limited phía ZenMux; thêm gate phía ta để 1 user không nuốt hết quota + chặn lạm dụng.

---

## 9. Error handling & độ bền

| Tình huống | Xử lý |
|---|---|
| ZenMux timeout / 5xx / rate-limited | Retry có backoff (vd Polly, 2 lần); hết retry → trả lời lịch sự "trợ lý đang bận, thử lại sau", **không** lộ chi tiết lỗi |
| LLM gọi tool không tồn tại / args sai schema | Registry trả lỗi có cấu trúc về LLM để nó tự sửa; quá `MaxToolHops` → dừng, trả thông báo an toàn |
| MediatR trả `Result.Failure` | Map Code+Message về LLM để diễn giải cho user (không ném exception) |
| Context vượt window | Cắt history theo token-window (§6) |

---

## 10. Test plan

- **Unit:** `AssistantToolRegistry` dispatch đúng record + inject UserId/MarketId; `ConfirmationGate` chặn confirm khi thiếu cờ; map `Result.Failure` → JSON lỗi. (LLM client mock.)
- **Integration:** `POST /assistant/chat` với `IAssistantChatClient` **fake** (kịch bản tool-call scripted) — kiểm ttoàn luồng: search → create_draft → preview → confirm-with-flag, KHÔNG gọi GLM thật trong CI.
- **Manual/demo:** smoke test với GLM 5.2 thật bằng tài khoản free.
- Mục tiêu coverage 80% cho phần orchestrator/gate/registry (LLM provider impl mock).

---

## 11. Quyết định đã chốt (2026-06-22)

1. **Streaming:** ✅ **CHỐT — non-streaming cho v1** (trả nguyên câu). 📌 **Note giai đoạn sau:** bổ sung **SSE streaming** (stream token real-time, UX mượt) — xem §11a roadmap.
2. **Ngôn ngữ:** system prompt + reply **tiếng Việt** mặc định.
3. **Phạm vi tool v1:** ✅ **CHỐT — MVP 5 tool** (xem §12 T2), mở rộng full 10 tool ở giai đoạn sau.
4. **Conversation store:** ✅ **CHỐT — DB (Postgres) cho v1**, **chưa** dùng Redis (tránh dựng thêm hạ tầng). Đổi sang Redis = drop-in sau nhờ `IConversationStore` — xem §11a.
5. **Fallback khi hết quota free:** **báo lỗi v1**, để ngỏ swap provider nhờ `IAssistantChatClient`.

### 11a. Roadmap giai đoạn sau (out-of-scope v1, ghi lại để khỏi quên)

| Hạng mục | Mô tả | Ghi chú |
|---|---|---|
| **SSE streaming** | Stream token real-time thay vì trả nguyên câu. Cần: endpoint SSE (hoặc tái dùng SignalR), xử lý tool-call *giữa* stream, client render từng chunk. `IAssistantChatClient` sẽ thêm `CompleteStreamingAsync(...)`. | Ưu tiên cao nhất nhóm "sau" — UX |
| Full 10 tool | Bổ sung add/update/remove item, list_orders, reorder_from_history | Mở rộng từ MVP 5 |
| Provider dự phòng | Cấu hình fallback model trả phí khi free-tier hết quota | Đã để ngỏ qua interface |
| **Redis conversation store** | Đổi `DbConversationStore` → `RedisConversationStore` (sliding TTL tự động, scale-out). Drop-in nhờ `IConversationStore`, không đụng orchestrator | Khi cần scale/perf |
| Cleanup job hết hạn | Background job xóa row `expires_at < now()` (giống `PartitionMaintenanceJob`) | DB-store dùng lazy-expire trước |
| Index ILIKE | SCRUM-239 — index product name tối ưu search | XS, optional |

---

## 12. Đề xuất task breakdown (chờ SCRUM key — KHÔNG commit trước khi có key)

| # | Task | Module/Folder | Effort |
|---|---|---|---|
| T1 | `IAssistantChatClient` + `ZenMuxChatClient` (Microsoft.Extensions.AI) + options/secret | `Assistant/Llm` | M |
| T2 | `AssistantTool` + `AssistantToolRegistry` + `ToolDefinitions` (map 5 MVP tool) | `Assistant/Tools` | M |
| T3 | `IConversationStore` + `RedisConversationStore` + `ConversationState` | `Assistant/Conversation` | S |
| T4 | `ConfirmationGate` (two-phase safety) | `Assistant/Safety` | S |
| T5 | Orchestrator loop + `AssistantController` + DTOs + `AddAssistant()` wiring | `Assistant/` | M |
| T6 | Rate-limit policy `assistant` + config validation startup | `Assistant/DI` + `Program.cs` | S |
| T7 | Unit + integration tests (fake LLM client) | `tests/` | M |
| T8 | (optional) SCRUM-239 index product name cho ILIKE — tối ưu search | Pricing.Infrastructure | XS |

> Tuân thủ: **không tự commit khi chưa có Jira key**; commit theo key task con; format gate qua `FreshFlow.slnx`.

---

## 13. Tóm tắt

GLM 5.2 (Free) qua ZenMux **khả thi** cho Tầng 2 vì: (a) OpenAI-compatible → .NET cắm gọn qua `Microsoft.Extensions.AI`; (b) hỗ trợ tool-calling — điều kiện sống còn để orchestrate các MediatR command/query của Tầng 1; (c) miễn phí, đủ cho capstone/demo. Kiến trúc giữ **provider-agnostic** (`IAssistantChatClient`) để swap model trả phí về sau, và đặt **safety gate two-phase** đảm bảo không bao giờ auto-confirm đơn. Rủi ro chính: rate-limit free-tier (chỉ demo) + dữ liệu nhạy cảm qua gateway bên thứ 3 (đã có nguyên tắc lọc PII/công nợ khỏi prompt).
