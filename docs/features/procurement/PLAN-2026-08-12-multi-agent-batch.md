# PLAN — Nhiều market agent / một phiên chợ (chia danh sách hàng)

- **Ngày:** 2026-08-12
- **Epic:** Procurement (SCRUM-256 dòng phiên chợ)
- **Quyết định:** Mô hình **B — chia danh sách hàng** (mỗi agent nhận một phần item), admin gán/gỡ.
- **Trạng thái:** Draft, chờ Jira key.

## 1. Vấn đề

Hiện `ProcurementBatch` chỉ có **một** `AssignedAgentUserId`. Một phiên chợ lớn cần nhiều
agent cùng đi mua, mỗi người phụ trách một phần mặt hàng. Cần cho phép chia danh sách item của
phiên cho nhiều agent, mỗi agent chỉ mua/báo thiếu phần của mình; phiên chỉ bàn giao khi **mọi
phần** đã xong.

## 2. Mô hình dữ liệu

Quyền sở hữu chuyển từ **phiên** xuống **từng item**.

- `ProcurementBatchItem` thêm `AssignedAgentUserId` (Guid?, nullable) + `AssignedAt` (DateTime?).
- Tập agent của phiên = `Items.Select(i => i.AssignedAgentUserId).Where(!= null).Distinct()`.
- Cột phiên `procurement_batches.assigned_agent_user_id` / `assigned_at`: **giữ nguyên 1 chu kỳ
  migration** làm nguồn backfill + back-compat, **ngừng dùng để gate**. Đánh dấu deprecated trong
  code, drop ở migration sau khi FE chuyển xong. (Không drop cùng migration này — DB `5433` là
  PROD, giảm rủi ro.)

## 3. Thay đổi Domain (`ProcurementBatch`)

| Method cũ | Method mới | Ghi chú |
|---|---|---|
| `AssignAgent(agentUserId)` | `AssignItems(IReadOnlyDictionary<Guid marketProductId, Guid agentUserId>, assignedAtUtc)` | Gán/gán-lại nhiều item cho nhiều agent trong 1 lần |
| `ConfirmPurchase(lines)` | `ConfirmPurchase(agentUserId, lines)` | Xác nhận **một phần** — chỉ item của agent đó |
| `ReportException(...reportedByUserId...)` | + guard | item phải thuộc `reportedByUserId` |
| `HandoverToHub()` | `HandoverToHub(agentUserId)` | + invariant "mọi phần xong"; event mang `HandedOffByUserId = agentUserId` |

### 3.1 `AssignItems`
- Cho phép khi Status ∈ {Manifested, Purchasing}; từ chối item **đã** `PurchasedAt != null`.
- Mỗi `marketProductId` phải nằm trong phiên.
- Set `item.AssignedAgentUserId` + `AssignedAt`.
- Raise `ProcurementAgentAssignedDomainEvent` cho **mỗi agent mới xuất hiện** (giữ event cũ; nó
  chỉ mang Batch + Agent + Market → không đổi shape).

### 3.2 `ConfirmPurchase(agentUserId, lines)` — xác nhận từng phần
- `requiredItems` = item **non-exempt** có `AssignedAgentUserId == agentUserId`.
- `lines` phải khớp đúng tập đó (thừa/thiếu/không thuộc agent → `PURCHASE_LINES_MISMATCH`).
- Xác nhận các item đó; item Unavailable-exception thì `ClearPurchase()` như hiện tại.
- Lần confirm-phần **đầu tiên** từ `Manifested` → flip Status = `Purchasing`. Các lần sau giữ
  `Purchasing`.

### 3.3 `HandoverToHub(agentUserId)` — invariant mới
- `agentUserId` phải thuộc tập agent của phiên.
- Chặn nếu còn item **non-exempt** chưa settle (chưa `PurchasedAt` và không có exception
  `Unavailable`) → `BATCH_INCOMPLETE`.
- Phần lifecycle Purchasing→HandedOff giữ nguyên. Event `HandedOffByUserId = agentUserId`.

## 4. Application / Handlers

- `AssignAgentCommand` → `AssignBatchItemsCommand(BatchId, IReadOnlyList<(Guid MarketProductId,
  Guid AgentUserId)>)`. Handler: `IsEligibleMarketAgentAsync` cho **mỗi agent distinct** vs
  `batch.MarketId`; rồi `AssignItems`.
- `ConfirmPurchase` / `ReportException` / `Handover` handler: đổi gate
  `AssignedAgentUserId != request.AgentUserId` → **`!batch.Items.Any(i => i.AssignedAgentUserId ==
  request.AgentUserId)`** (agent không giữ item nào trong phiên → 404). Truyền `agentUserId` vào
  domain.
- `GetAssignedProcurementTask(s)`: gate/list theo `Items.Any(i => i.AssignedAgentUserId == agent)`.
  Repo `ListByAgentAsync` đổi filter tương ứng. Detail trả **cả** phiên nhưng DTO item mang
  `AssignedAgentUserId` để FE lọc phần của mình.

## 5. DTO

- `ProcurementBatchItemDto` + `AssignedAgentUserId`.
- `AssignBatchItemsRequest(IReadOnlyList<ItemAssignmentDto>)`, `ItemAssignmentDto(Guid
  MarketProductId, Guid AgentUserId)`.

## 6. DB migration

1. `ALTER TABLE procurement_batch_items ADD assigned_agent_user_id uuid NULL, ADD assigned_at
   timestamptz NULL`.
2. Backfill: copy `b.assigned_agent_user_id` → mọi item của batch đó.
3. Index filtered `WHERE assigned_agent_user_id IS NOT NULL`.
4. Cập nhật `AppDbContextModelSnapshot`. **Không** drop cột phiên trong migration này.

## 7. Admin API

- `PUT /admin/procurement/batches/{batchId}/item-assignments`
  body = `[{ marketProductId, agentUserId }]`. Idempotent — ghi đè phân công. Gỡ = gửi map không
  chứa item đó **hoặc** `agentUserId = null` (chốt: null để clear).
- Bỏ `POST .../assign-agent` cũ (hoặc giữ như alias gán-toàn-bộ-cho-1-người trong 1 sprint chuyển
  tiếp — tùy FE).

## 8. ⚠️ Rủi ro cần chốt trước khi code

1. **Concurrency token.** Hai agent confirm hai phần khác nhau **đồng thời** cùng đập vào
   `procurement_batches.updated_at` (IsConcurrencyToken) → người thứ 2 fail concurrency dù sửa item
   khác nhau. **Mitigation (ponytail):** confirm-phần chỉ bump `item.UpdatedAt`, **không** đụng
   token của batch; chỉ lần flip Status = Purchasing mới ghi batch. Cần verify EF không tự bump
   parent khi chỉ đổi child.
2. **Handover invariant** đổi hành vi FE: giờ trả `409 BATCH_INCOMPLETE` nếu còn phần chưa mua.
   FE phải hiển thị tiến độ theo agent.
3. **Prod DB 5433** — migration chỉ add nullable + copy nên an toàn; xác nhận trước khi apply.
4. **Eligibility N agent** — vòng lặp gọi reader; giữ nguyên seam `IMarketAgentReader`, không thêm
   seam mới.

## 9. Test

- **Unit (Domain):** AssignItems phân phối đúng + từ chối item đã mua; ConfirmPurchase-phần khớp
  đúng tập của agent, reject item chéo agent; Handover chặn tới khi mọi phần settle; ReportException
  chặn item không thuộc agent.
- **Integration (Postgres):** eligibility theo từng agent; 2 agent confirm 2 phần rồi 1 agent
  handover; seam `item.assigned_agent_user_id` (ToSqlQuery không chạy trên InMemory).

## 10. Không làm (YAGNI)

- Agent tự join phiên (đã chốt: chỉ admin gán).
- Tự động chia đều item cho agent — admin chỉ định thủ công.
- Theo dõi realtime tiến độ per-agent qua SignalR — dùng poll `GetAssignedProcurementTask` trước.
