# PLAN — Phiên chợ 360 (Batch Overview read-model)

**Date:** 2026-08-13
**Module:** Procurement
**Type:** Read-model / query (KHÔNG đổi aggregate, KHÔNG phá ranh giới module)

---

## Mục tiêu

Biến phiên chợ (`ProcurementBatch`) thành **điểm nhìn hợp nhất**: từ một `batchId`, xem
được mọi thứ về phiên đó trong một response — đơn nó phủ + trạng thái giao, hiệu suất từng
market agent, tiến độ nhận/phân loại tại hub, và tài chính (hóa đơn + công nợ phát sinh).

**Nguyên tắc bất biến:** mỗi module vẫn sở hữu dữ liệu của nó. Batch KHÔNG "sở hữu" order /
delivery / hub-event / invoice. Fan-out key luôn là `orderIds` của batch (từ join
`ProcurementBatchOrder`) hoặc `batchId`. Không nhồi `BatchId` vào bảng module khác.

## Không làm (đã bác)

- God aggregate: dời orders/deliveries/hub/invoice vào trong `ProcurementBatch`. Vi phạm ranh
  giới tuyệt đối của CLAUDE.md (Procurement.Domain/Application không ref module khác).

---

## Endpoint

`GET /api/v1/procurement/batches/{batchId:guid}/overview` → `Result<ProcurementBatchOverviewDto>`

Đặt trong **Procurement module**, route qua `ISender` như mọi endpoint khác. RBAC:
`admin` + `operations_manager` (xem lại seed trước khi gắn `[Authorize]`).

---

## Phân pha (mỗi pha là 1 endpoint chạy được)

| Pha | Nội dung | Seam mới |
|---|---|---|
| **1** | Header + Hiệu suất agent + Đơn (status qua reader đã có) | **0** |
| 2 | Hub inbound/sorting theo batchId | +1 (Hub) |
| 3 | Trạng thái giao từng đơn | +1 (Logistics) |
| 4 | Tài chính: hóa đơn + công nợ | +2 (Invoicing, Orders credit) |

Ship Pha 1 trước, review, rồi mới làm tiếp.

---

## Pha 1 — chi tiết (0 seam)

Toàn bộ dữ liệu Pha 1 đã có sẵn: aggregate `ProcurementBatch` (Items/Orders/Exceptions) +
`IConfirmedOrderReader.ReadStatusesAsync(orderIds)` (đã tồn tại).

### DTO — `Dtos/ProcurementBatchOverviewDto.cs`

```
ProcurementBatchOverviewDto(
  // Header — từ chính aggregate
  Guid BatchId, string? Code, Guid MarketId, Guid? HubId, DateOnly BatchDate,
  string Status, DateTime? ManifestedAt, DateTime? HandedOffAt, DateTime? CompletedAt,
  DateTime? CancelledAt, string? CancellationReason,
  int TotalItemCount, int ItemsPurchased, int ItemsPending, int ExceptionCount,

  IReadOnlyList<BatchAgentPerformanceDto> Agents,
  IReadOnlyList<BatchOrderStatusDto> Orders)

BatchAgentPerformanceDto(
  Guid AgentUserId,
  int ItemsAssigned, int ItemsPurchased, int ItemsPending,
  int ExceptionsReported,
  decimal? ReferenceCostTotal,   // Σ ReferenceUnitPrice * (ActualQty ?? Quantity)
  decimal? ActualCostTotal,      // Σ ActualUnitPrice * ActualQuantity (chỉ item đã mua)
  decimal? VarianceTotal)        // ActualCostTotal - ReferenceCostTotal (null nếu thiếu vế)

BatchOrderStatusDto(Guid OrderId, string? Status)   // Status từ ReadStatusesAsync
```

### Query
`Queries/GetBatchOverview/` — `GetBatchOverviewQuery(Guid BatchId)`, Handler, Validator
(NotEmpty BatchId). Copy cấu trúc từ `GetProcurementProgress/`.

### Handler
1. Load batch-by-id (dùng đúng method repo mà `GetAssignedProcurementTaskQueryHandler` dùng —
   phải kèm Items/Orders/Exceptions). Null → `Result.Failure(Error.NotFound("PROCUREMENT_BATCH_NOT_FOUND", ...))`.
2. Header + counts: theo đúng logic `GetProcurementProgressQueryHandler.MapBatch`
   (itemsPurchased = ActualQuantity != null; itemsPending = 0 nếu Cancelled).
3. **Agent perf:** group `batch.Items` theo `AssignedAgentUserId` (bỏ null). Với mỗi nhóm:
   đếm assigned/purchased/pending; exceptions = `batch.Exceptions` (không IsDeleted) có
   `ReportedByUserId == agent`; cost totals guard chia/null theo CLAUDE.md
   (`Sum(x => (decimal?)...) ?? ...`, không chia khi thiếu vế → null).
4. **Orders:** `ReadStatusesAsync(orderIds)`; map từng orderId → status (GetValueOrDefault).

### Test (bắt buộc)
- **Unit** (`FreshFlow.Procurement.UnitTests`): agent grouping + variance + counts +
  batch-not-found. Fake `IConfirmedOrderReader`.
- **Integration** (`FreshFlow.IntegrationTests/Procurement`): seed 1 batch multi-agent +
  vài order, GET overview, assert shape. (Reader status seam đã có nên chạy trên Postgres thật.)

### Ceiling / hoãn có chủ đích
- **Tên agent:** Pha 1 trả `AgentUserId` thô. `IMarketAgentReader` chỉ check eligibility, chưa
  có tên. Thêm reader tên agent khi UI cần — đừng làm trước. (`ponytail:` note trong DTO.)
- `AssignedAgentUserId`/`AssignedAt` trên batch đã deprecated (per-item thay thế) — KHÔNG đưa
  vào DTO overview. Dọn field khỏi entity để migration riêng, ngoài phạm vi pha này.
