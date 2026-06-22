# Task Breakdown — AI Assistant Thin Queries (3 gaps)

| | |
|---|---|
| **Ngày lập** | 2026-06-20 |
| **Branch** | `SCRUM-179-ORD-Order-Management` |
| **Nguồn** | `docs/SURVEY-2026-06-18-ai-shopping-assistant-feasibility.md` §1a, §3, §7 |
| **Epic/Task IDs** | Xem `docs/BACKLOG-2026-06-20-ai-assistant-thin-queries.md` (registry + SCRUM key) |
| **Phạm vi** | CHỈ 3 thin read-side query, build module-side. **KHÔNG** đụng orchestrator/host/LLM. |
| **Trạng thái** | ✅ Có SCRUM key (epic SCRUM-234, task con 235–246) → commit per-task theo key con. |

> **Definition of Done (chung mọi task):** TDD (RED→GREEN→refactor), coverage ≥ 80%, `dotnet format FreshFlow.sln --verify-no-changes` sạch, không hardcode secret, reviewer pass. Sau mỗi lệnh `dotnet` one-shot chạy `dotnet build-server shutdown`.

---

## EPIC ASSIST-E1 — `SearchMarketProductsQuery` (Pricing) — Effort M

Free-text search theo tên SP + giá + tồn, scope theo `MarketId`, một round-trip (EF join Catalog `products`, cursor-paginated). Task độc lập, song song được với ASSIST-E3.

**Quyết định module (survey §1a):** thuộc **Pricing** — output cần là price + availability (`available = current − reserved`), dữ liệu Pricing sở hữu (`market_products`); join sẵn tới Catalog `products` cho tên (đã có ở projection `MarketProductRow`). KHÔNG đặt ở Catalog (sẽ buộc Catalog phụ thuộc dữ liệu Pricing — sai chiều).

| Task | Mô tả / Acceptance | Test (RED trước) | Dep |
|---|---|---|---|
| **ASSIST-E1-T1** DTOs | `MarketProductSearchItemDto(MarketProductId, ProductId, ProductName, Category?, CurrentPrice, AvailableQuantity)` + `SearchMarketProductsResultDto(Items, NextCursor)`. `AvailableQuantity = current − reserved` (khớp `IMarketProductReader.FindAsync`). Vị trí: `Pricing.Application/Queries/SearchMarketProducts/`. | — | — |
| **ASSIST-E1-T2** Query + Validator | `SearchMarketProductsQuery(MarketId, SearchText, Category?, InStockOnly=false, Cursor?, PageSize=20) : IQuery<Result<SearchMarketProductsResultDto>>`. Validator: `MarketId` required; `SearchText` non-empty + max length; `PageSize` 1..100. | Validator tests: empty text, bad pageSize, missing market | T1 |
| **ASSIST-E1-T3** Repo method | `IMarketProductRepository.SearchAsync(criteria, ct)` + EF impl: `market_id=@MarketId` AND `products.name ILIKE %SearchText%` AND active AND not soft-deleted; `InStockOnly ⇒ current−reserved>0`; project `available`; cursor order + page. Reuse projection shape `MarketProductRow`. **No N+1.** | Repo test: match by name, market scoping, InStockOnly, cursor paging | T1 |
| **ASSIST-E1-T4** Handler | `internal sealed` handler → repo → map DTO → `Result`. | Handler tests: happy path, empty result, cursor next | T2, T3 |
| **ASSIST-E1-T5** Index (optional) | Cân nhắc index `products.name` cho ILIKE. Functionally optional v1 → task tách, không block. | — | T3 |

**Ràng buộc:** KHÔNG đụng `IMarketProductReader` (single-product point lookup, thuộc Orders cross-module — sai module/cardinality). Repo mới nằm trên `MarketProduct` entity của Pricing.

---

## EPIC ASSIST-E2 — Market context / picker (Catalog) — Effort XS

Quyết định **2-A** đã chốt (survey §1a Query 2): market chọn ở app-shell, assistant chỉ đọc `MarketId` client gửi. `GetMarketsQuery` đã tồn tại; `GET /api/v1/markets` mở cho mọi authenticated user.

| Task | Mô tả / Acceptance | Dep |
|---|---|---|
| **ASSIST-E2-T1** Verify + document | Test xác nhận `GET /api/v1/markets` không có role/agent gate; document rằng picker reuse `GetMarketsQuery` as-is, assistant consume `MarketId` từ transport. **Zero new backend code.** Nếu test phát hiện có gate ngoài dự kiến → **escalate, không tự sửa** (giả định nền của 2-A). | — |

> Option 2-B (binding restaurant→market thật) **defer** — cần schema + Auth-module change + product sign-off (effort L). Không làm ở đây.

> **ASSIST-E2-T1 — kết quả verify (2026-06-20):** Xác nhận `MarketsController` có `[Authorize]` ở class level, KHÔNG có `[Authorize(Roles=...)]` thêm trên `GetMarketsAsync`/`GetMarketByIdAsync` — mọi role authenticated (admin, driver, market_agent, restaurant...) đều 200. Không có gate ngoài dự kiến → giả định nền 2-A đứng vững, không cần escalate. `GetMarketsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<MarketDto>>` (Catalog) không nhận `MarketId` — nó list toàn bộ market để app-shell/assistant chọn; `MarketId` chỉ xuất hiện ở các endpoint downstream (`SearchMarketProducts`, `GetMarketProducts`...) nhận từ transport sau khi user đã chọn. Reuse plan cho assistant: gọi `GET /api/v1/markets` không tham số/sửa đổi gì, picker UI hiển thị danh sách, gửi `MarketId` đã chọn vào các query khác — không cần adapter hay endpoint mới.

---

## EPIC ASSIST-E3 — `PreviewOrderConfirmationQuery` (Orders) — Effort M

Dry-run confirm: surface lỗi (`CREDIT_LIMIT_EXCEEDED`, `DELIVERY_DATE_OUT_OF_WINDOW`, `ORDER_EMPTY`...) **không mutate state**. Gồm refactor "extract evaluator" + query mới.

| Task | Mô tả / Acceptance | Test | Dep |
|---|---|---|---|
| **ASSIST-E3-T0** Spike credit repr. | Verify cách `CreditService.CanChargeAsync` biểu diễn over-limit. **Xác nhận lại (2026-06-20): đúng là `Result.Failure(CREDIT_LIMIT_EXCEEDED)`** — xem `src/Modules/Orders/FreshFlow.Orders.Application/Services/CreditService.cs:25` (`if (!account.CanCharge(amount)) return Result<CreditCheckDto>.Failure(CreditLimitExceeded(account, amount));`), KHÔNG phải `Success(CanCharge=false)`. `CreditCheckDto.CanCharge` chỉ tồn tại trong DTO thuộc nhánh `Success` (luôn `true` ở đó); nhánh over-limit không bao giờ tạo DTO. **Đối chiếu thêm `ConfirmOrderCommandHandler` hiện tại** (`Commands/ConfirmOrder/ConfirmOrderCommandHandler.cs`): thứ tự check thật là load (404) → ownership (FORBIDDEN) → `order.CanConfirm()` (`ORDER_NOT_DRAFT` / `ORDER_EMPTY`, từ `Order.cs:114`) → `creditService.CanChargeAsync` (short-circuit `Result.Failure`) → cutoff/window (`DELIVERY_DATE_OUT_OF_WINDOW`) → reschedule/Confirm/Charge. Quyết định (giữ nguyên): evaluator nhận `CreditCheckDto` (success-only, do caller gọi `CanChargeAsync` trước và short-circuit khi `Result.Failure` — evaluator không tự gọi credit service); preview dùng adapter nhỏ để chuyển `Result.Failure(CREDIT_LIMIT_EXCEEDED)` thành `PreviewIssueDto(Code, Message)` thay vì coi là lỗi hệ thống. | — | — |
| **ASSIST-E3-T1** Extract evaluator | New `internal static OrderConfirmationEvaluator` tại `Orders.Application/Services/`. `Evaluate(Order, CreditCheckDto, DateTime nowUtc) → OrderConfirmationEvaluation(Issues, ResolvedScheduledFor, TotalAmount, CreditCheck)`. Pure, non-mutating; gom `CanConfirm` + credit verdict + cutoff/window. **Append issues đúng thứ tự check hiện tại** (CanConfirm → credit → window) ⇒ `Issues[0]` = first-error cũ. | Evaluator unit tests từng issue code + thứ tự | T0 |
| **ASSIST-E3-T2** Refactor confirm handler | `ConfirmOrderCommandHandler`: load+ownership giữ nguyên → `CanChargeAsync` → `Evaluate` → fail-fast `Issues[0]` → mutation tail (`RescheduleFor`/`Confirm`/`ChargeAsync`) **byte-for-byte như cũ**. | **7 test ConfirmOrder phải GREEN, không sửa** (regression guard) | T1 |
| **ASSIST-E3-T3** Preview DTOs | `OrderConfirmationPreviewDto(WouldSucceed, Issues, TotalAmount, ResolvedScheduledFor, RemainingCreditAfter?)` + `PreviewIssueDto(Code, Message)`. | — | — |
| **ASSIST-E3-T4** Query + handler | `PreviewOrderConfirmationQuery(UserId, OrderId)`. Handler: load (404) + ownership (FORBIDDEN) như confirm → `CanChargeAsync` → `Evaluate` → map **full** `Issues` list; `RemainingCreditAfter = AvailableCredit − TotalAmount` (no new repo read). 404/FORBIDDEN trả `Result.Failure` (không phải "preview issue"). | Handler tests: preview khớp confirm outcome qua từng issue code; 404/forbidden | T1, T3 |
| **ASSIST-E3-T5** Controller endpoint | `GET /api/v1/orders/{orderId}/confirm-preview` (role `restaurant`, dưới policy rate-limit `orders`). Map `Result → ApiResponse`. | Controller smoke/integration | T4 |

---

## Thứ tự thực thi & song song

```
Luồng 1 (Pricing, ASSIST-E1):  T1 → T2/T3 → T4        [T5 optional, tách]
Luồng 2 (Catalog, ASSIST-E2):  T1  (XS, bất kỳ lúc nào)
Luồng 3 (Orders, ASSIST-E3):   T0 → T1 → T2 (gate: 7 tests GREEN) → T3/T4 → T5

Gate cứng: ASSIST-E3-T2 KHÔNG merge nếu test ConfirmOrder đổi/đỏ.
```

---

## Rủi ro cần coder lưu ý

- **ASSIST-E3-T2 là refactor "pure"** — bất kỳ thay đổi error code/precedence nào ở confirm = FAIL review.
- **ILIKE không index** (ASSIST-E1-T3) có thể chậm trên data lớn → T5 theo sau; v1 chấp nhận.
- **Over-allocation** (Redis reservation deferred): `AvailableQuantity` là best-effort, KHÔNG phải hold — đừng thêm logic giữ chỗ.
- **Không** thêm business logic vào host/orchestrator (chưa làm ở đây); 3 query này thuần read trong module sở hữu (giữ Option-B honest, dễ migrate A2 sau).

---

## Điều kiện trước khi giao team backend-dev

1. **Cấp SCRUM key** cho từng Epic/Task (xem `BACKLOG-2026-06-20-...md`).
2. Cập nhật bảng commit-map dưới đây với key thực.

### Commit map (key cấp 2026-06-20 — epic SCRUM-234, commit dùng key task con)

| Task code | SCRUM Key | Commit prefix | Trạng thái commit |
|---|---|---|---|
| ASSIST-E1-T1 | SCRUM-235 | `feat(pricing): SCRUM-235 ...` | ✅ `1399ce3` |
| ASSIST-E1-T2 | SCRUM-236 | `feat(pricing): SCRUM-236 ...` | ✅ `a5246f1` |
| ASSIST-E1-T3 | SCRUM-237 | `feat(pricing): SCRUM-237 ...` | ✅ `b751a4e` |
| ASSIST-E1-T4 | SCRUM-238 | `feat(pricing): SCRUM-238 ...` | ✅ `a1c503e` |
| ASSIST-E1-T5 (optional) | SCRUM-239 | `perf(pricing): SCRUM-239 ...` (khi implement) | 🔲 chưa làm |
| ASSIST-E2-T1 | SCRUM-240 | `test(catalog): SCRUM-240 ...` | ✅ `4d946a3` (doc-note → SCRUM-234) |
| ASSIST-E3-T0 | SCRUM-241 | (no code commit — Option B) | ⛔ no-code-commit; finding ghi ở doc này |
| ASSIST-E3-T1 | SCRUM-242 | `feat(orders): SCRUM-242 ...` | ✅ `7466362` |
| ASSIST-E3-T2 | SCRUM-243 | `refactor(orders): SCRUM-243 ...` | ✅ `aa526e5` |
| ASSIST-E3-T3 | SCRUM-244 | `feat(orders): SCRUM-244 ...` | ✅ `3217faf` |
| ASSIST-E3-T4 | SCRUM-245 | `feat(orders): SCRUM-245 ...` | ✅ `f66d36b` |
| ASSIST-E3-T5 | SCRUM-246 | `feat(orders): SCRUM-246 ...` | ✅ `50e3ba6` |

> **3 file planning (SURVEY/TASKS/BACKLOG)** = docs cấp epic, KHÔNG stage vào commit task con. Commit MỘT LẦN ở cuối epic dưới key epic **SCRUM-234**: `docs(orders): SCRUM-234 add AI assistant thin-query survey, backlog & task docs`. Đây là ngoại lệ hợp lệ duy nhất dùng key epic.

---

## Nhật ký tiến độ

| Ngày | Task | Sự kiện |
|---|---|---|
| 2026-06-20 | — | Lập breakdown từ survey; chờ SCRUM key trước khi giao team. |
| 2026-06-20 | ASSIST-E3-T0 | Spike đóng: xác nhận `CanChargeAsync` trả `Result.Failure(CREDIT_LIMIT_EXCEEDED)` cho over-limit (không phải `Success(CanCharge=false)`); đối chiếu thứ tự check thật trong `ConfirmOrderCommandHandler` (load → ownership → `CanConfirm` → credit → window). Không đổi production code. Kiến trúc evaluator (T1) giữ nguyên quyết định: success-only `CreditCheckDto` + caller short-circuit + adapter cho preview. |
| 2026-06-20 | ASSIST-E3-T5 | PASS + commit `50e3ba6` (SCRUM-246, coder2). GET /api/v1/orders/{orderId}/confirm-preview, role restaurant, policy orders. 349/349 GREEN, regression gate ConfirmOrderCommandHandler vẫn untouched end-to-end. ⇒ HOÀN TẤT 11/11 task (E1/E2/E3). |
| 2026-06-20 | ASSIST-E3-T2 | PASS gate + commit `aa526e5` (SCRUM-243). Regression gate giữ: KHÔNG sửa test ConfirmOrder, mutation tail byte-for-byte, full Orders suite GREEN. Handler giờ gọi OrderConfirmationEvaluator→Issues[0] fail-fast. |
| 2026-06-20 | ASSIST-E1-T4 | PASS + commit `a1c503e` (SCRUM-238). Epic ASSIST-E1 core path (T1→T4) hoàn tất. Reviewer LOW (không block, follow-up tùy chọn): handler không check market-existence (interface chưa có `MarketExistsAsync`) ⇒ MarketId không tồn tại → empty list ở đây nhưng 404 ở `GetMarketProducts` — inconsistency UX nhỏ, không phải defect. |
| 2026-06-20 | ASSIST-E1-T1/T2 | PASS review + commit: `1399ce3` (SCRUM-235 DTOs), `a5246f1` (SCRUM-236 query+validator). Staging per-task, không đụng file pre-existing/planning. |
| 2026-06-20 | ASSIST-E3-T0 | Quyết định commit (supervisor): **Option B** — spike không có code commit; finding ghi ở doc này. SCRUM-241 = no-code-commit. |
| 2026-06-20 | — | 3 file planning (SURVEY/TASKS/BACKLOG) = docs cấp epic → commit MỘT LẦN ở cuối epic dưới key epic SCRUM-234 (ngoại lệ hợp lệ duy nhất dùng key epic). |
| 2026-06-20 | ASSIST-E1-T3 | Implement xong, gửi reviewer (chưa commit, chờ pass + key SCRUM-237 đã có ở backlog). `IMarketProductRepository.SearchAsync` + EF impl: join `ProductDetailRow` (Catalog cross-module, loại sản phẩm soft-delete), `EF.Functions.ILike` có escape wildcard, `InStockOnly` filter, cursor (base64 JSON `{id, createdAt}`) phân trang theo `(CreatedAt, Id)`, fetch `pageSize+1` để biết `NextCursor`. 5/5 integration test mới GREEN (match tên, market scoping, InStockOnly, cursor 2 trang, soft-delete exclude); 55/55 Pricing integration test cũ vẫn GREEN (no regression). Bug đã fix: cột DB là `deleted_at` (snake_case override), không phải `DeletedAt`. `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên 3 file đổi. |
| 2026-06-20 | ASSIST-E1-T3 | Reviewer PASS — task #3 marked completed. |
| 2026-06-20 | ASSIST-E3-T1 | Implement xong, gửi reviewer (chưa commit). `internal static OrderConfirmationEvaluator.Evaluate(Order, CreditCheckDto, DateTime nowUtc) → OrderConfirmationEvaluation(Issues, ResolvedScheduledFor, TotalAmount, CreditCheck)` tại `Orders.Application/Services/`. Pure, không mutate `Order`. `Issues` là `IReadOnlyList<Error>`, append đúng thứ tự check hiện tại trong `ConfirmOrderCommandHandler`: `order.CanConfirm()` (ORDER_NOT_DRAFT/ORDER_EMPTY) → window (DELIVERY_DATE_OUT_OF_WINDOW, qua `OrderCutoffScheduler.ResolveScheduledFor`+`IsWithinDeliveryWindow`). Credit không tự check trong evaluator — theo quyết định T0, caller gọi `CanChargeAsync` và short-circuit `Result.Failure` trước khi vào evaluator nên `CreditCheckDto` truyền vào luôn là success-path; evaluator chỉ thread nó qua để preview tính `RemainingCreditAfter` sau (T4), không tạo issue từ nó. 7/7 evaluator unit test GREEN (no-issue happy path, ORDER_NOT_DRAFT first, ORDER_EMPTY first, DELIVERY_DATE_OUT_OF_WINDOW, cutoff reschedule resolves ScheduledFor, non-draft+out-of-window vẫn trả ORDER_NOT_DRAFT trước window theo đúng thứ tự legacy, no-mutation check). 335/335 Orders unit test cũ vẫn GREEN (no regression). `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên 2 file đổi. |
| 2026-06-20 | ASSIST-E3-T1 | Reviewer PASS — task #7 marked completed. |
| 2026-06-20 | ASSIST-E2-T1 | Verify xong, gửi reviewer (zero new backend feature code, chỉ test + doc). 3 integration test mới (`GetMarketsAccessTests`): unauthenticated → 401; admin → 200; `driver` (role không có gate nào trên controller này) → 200, xác nhận `GET /api/v1/markets` chỉ có `[Authorize]` class-level, không có `[Authorize(Roles=...)]` thêm. Không phát hiện gate ngoài dự kiến → không escalate, giả định nền 2-A đứng vững. Document reuse: assistant gọi `GET /api/v1/markets` nguyên trạng để list, gửi `MarketId` đã chọn vào các query downstream — xem note ở mục EPIC ASSIST-E2 phía trên. `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên file test mới. |
| 2026-06-20 | ASSIST-E2-T1 | Reviewer PASS — task #5 marked completed. |
| 2026-06-20 | ASSIST-E3-T2 | Implement xong, gửi reviewer (chưa commit). `ConfirmOrderCommandHandler`: load+ownership+`CanConfirm()`+`CanChargeAsync` giữ nguyên; thay block window-check inline bằng `OrderConfirmationEvaluator.Evaluate(order, canChargeResult.Value, confirmedAtUtc)` → fail-fast `evaluation.Issues[0]` nếu có; `rescheduledFor` lấy từ `evaluation.ResolvedScheduledFor` thay vì gọi lại `OrderCutoffScheduler` trực tiếp. Mutation tail (`RescheduleFor`/`Confirm`/`Track`/`ChargeAsync`) byte-for-byte như cũ — diff xác nhận không đụng tới các dòng này. 9/9 `ConfirmOrderCommandHandlerTests` GREEN, không sửa file test; 335/335 Orders unit test suite vẫn GREEN (no regression). `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên file đổi duy nhất (`ConfirmOrderCommandHandler.cs`). `dotnet build FreshFlow.slnx` 0 error. |
| 2026-06-20 | ASSIST-E1-T4 | Implement xong, gửi reviewer (chưa commit). `internal sealed class SearchMarketProductsQueryHandler(IMarketProductRepository) : IRequestHandler<SearchMarketProductsQuery, Result<SearchMarketProductsResultDto>>` tại `Pricing.Application/Queries/SearchMarketProducts/`. Handler mỏng: map query fields → `MarketProductSearchCriteria` → gọi `repository.SearchAsync` → wrap `(Items, NextCursor)` thẳng vào `SearchMarketProductsResultDto` (repo đã trả đúng shape `MarketProductSearchItemDto`, không cần mapping riêng). Không check `MarketExistsAsync` (khác `GetMarketProductsQueryHandler`) vì `IMarketProductRepository` không có method đó và task spec không yêu cầu — market không tồn tại tự nhiên trả `Items` rỗng qua join. 4/4 handler unit test GREEN (happy path map đúng items+cursor, no-match rỗng, last-page cursor null, query fields map đúng vào criteria). 264/264 Pricing unit test cũ vẫn GREEN (no regression). MediatR auto-discover qua `RegisterServicesFromAssembly` — không cần đăng ký DI thủ công. `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên 2 file đổi. |
| 2026-06-20 | ASSIST-E3-T2 | Reviewer PASS — task #8 marked completed. |
| 2026-06-20 | ASSIST-E3-T3 | Implement xong, gửi reviewer (chưa commit). 2 record DTO mới tại `Orders.Application/Queries/PreviewOrderConfirmation/`: `PreviewIssueDto(string Code, string Message)` và `OrderConfirmationPreviewDto(bool WouldSucceed, IReadOnlyList<PreviewIssueDto> Issues, decimal TotalAmount, DateTime? ResolvedScheduledFor, decimal? RemainingCreditAfter)`. Không có logic (đúng spec "No logic") nên không thêm test riêng — đối chiếu codebase xác nhận không có precedent test cho pure DTO record ở cả Orders/Pricing module. `dotnet build` Orders.Application 0 error; 335/335 Orders unit test suite vẫn GREEN (no regression, không test nào đổi). `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên 2 file mới. |
| 2026-06-20 | ASSIST-E3-T3 | Reviewer PASS — task #9 marked completed. |
| 2026-06-20 | ASSIST-E3-T4 | Implement xong, gửi reviewer (chưa commit). `PreviewOrderConfirmationQuery(Guid UserId, Guid OrderId) : IQuery<OrderConfirmationPreviewDto>` + validator (NotEmpty cả 2 field, theo mẫu `GetOrderQueryValidator`) + `internal sealed class PreviewOrderConfirmationQueryHandler : IRequestHandler<PreviewOrderConfirmationQuery, Result<OrderConfirmationPreviewDto>>` tại `Orders.Application/Queries/PreviewOrderConfirmation/`. Handler mirror đúng load(404)+ownership(FORBIDDEN) như confirm, rồi gọi `CanChargeAsync`: (a) nếu `Result.Failure` (CREDIT_LIMIT_EXCEEDED) → KHÔNG fail query, adapter convert thẳng `Error` thành `PreviewIssueDto` duy nhất, `WouldSucceed=false`, `RemainingCreditAfter=null` (không có `CreditCheckDto` ở nhánh này nên không tính được mà không đọc lại repo — đúng yêu cầu "no new repo read"), `ResolvedScheduledFor=null` (chưa tới evaluator); (b) nếu `Result.Success` → gọi `OrderConfirmationEvaluator.Evaluate` lấy **toàn bộ** `Issues` (map thành `PreviewIssueDto`, không fail-fast như confirm) + `RemainingCreditAfter = evaluation.CreditCheck.AvailableCredit - evaluation.TotalAmount` (không đọc repo thêm, lấy thẳng từ `CreditCheckDto` đã có). Không mutate Order, không gọi `Track`/`ChargeAsync`. 9/9 test mới GREEN (404, forbidden, happy-path no-issue + RemainingCreditAfter đúng, non-draft issue, empty-order issue, out-of-window issue, non-draft+out-of-window → CẢ 2 issue cùng lúc (không chỉ issue đầu, khác hành vi confirm), credit-limit-exceeded → issue + RemainingCreditAfter null, no-mutation/no-charge check). 344/344 Orders unit test suite GREEN (335 cũ + 9 mới, no regression). `dotnet build FreshFlow.slnx` 0 error. `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên 4 file mới (Query/Validator/Handler/Tests). |
| 2026-06-20 | ASSIST-E3-T5 | Implement xong, gửi reviewer (chưa commit). `OrdersController`: thêm `GET /api/v1/orders/{orderId}/confirm-preview` (`[Authorize(Roles = "restaurant")]`, dưới `[EnableRateLimiting("orders")]` class-level hiện có), gọi `sender.Send(new PreviewOrderConfirmationQuery(ResolveUserId(), orderId), ct)`, map `Result → ApiResponse` y theo pattern `ConfirmOrderAsync`/`CancelOrderAsync` (`result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult()`). `[ProducesResponseType]` 200/403/404 (không có 409/422 vì preview không mutate, không throw conflict). Thêm `ProjectReference` tới `FreshFlow.API.csproj` trong `FreshFlow.Orders.UnitTests.csproj` (chưa có trước đây, cần để test controller trực tiếp — theo đúng pattern `FreshFlow.Catalog.UnitTests`). Test mới `PreviewOrderConfirmationControllerTests.cs` (5 test, theo mẫu `GetProductByIdControllerTests`): RBAC attribute đúng role `restaurant`; success no-issue → 200 + envelope đúng DTO; success with-issues (2 issue cùng lúc) → 200 + `WouldSucceed=false`; 404 khi order không tồn tại; 403 khi không phải owner (`FORBIDDEN`). 14/14 GREEN trong file mới + handler cũ; 349/349 Orders unit test suite GREEN (344 cũ + 5 mới, no regression). `dotnet build FreshFlow.slnx` 0 error. `dotnet format FreshFlow.slnx --verify-no-changes` sạch trên 3 file đổi (controller, csproj, test mới). |
