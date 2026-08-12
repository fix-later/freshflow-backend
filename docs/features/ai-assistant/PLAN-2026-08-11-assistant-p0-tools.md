# Plan: Mở rộng 6 tool P0 cho AI Assistant

**Ngày:** 2026-08-11
**Trạng thái:** Sẵn sàng triển khai
**Phạm vi:** Favorites, credit, lịch sử đơn hàng và địa chỉ giao hàng cho role `restaurant`.

## 1. Mục tiêu

Mở rộng assistant từ 5 tool hiện tại lên 11 tool bằng đúng 6 tool P0:

1. `list_favorites`
2. `add_favorite`
3. `remove_favorite`
4. `get_my_credit`
5. `list_my_orders`
6. `list_delivery_addresses`

Các tool tiếp tục dùng Application layer qua `ISender`; không truy cập database trực tiếp, không thêm package và không thay đổi endpoint `POST /api/v1/assistant/chat`.

## 2. Quyết định thiết kế

- Tái sử dụng query/command hiện có; không tạo module Assistant mới.
- `UserId`, `RestaurantId`, `MarketId` và quyền admin không bao giờ lấy từ LLM.
- Favorites là thao tác idempotent và có thể đảo ngược nên không cần confirmation gate mới. System prompt vẫn yêu cầu chỉ thêm/xóa khi user nói rõ ý định.
- `get_my_credit` không nhận arguments. Restaurant được suy ra từ `UserId` đã xác thực.
- `list_my_orders` luôn gửi `IsAdmin = false` và `RestaurantId = null`, để `ListOrdersQueryHandler` tự giới hạn về restaurant sở hữu user.
- Credit và địa chỉ/điện thoại là dữ liệu nhạy cảm: trả thẳng trong response có cấu trúc cho frontend, không đưa vào LLM, conversation history hoặc log.
- Giữ nguyên `MaxToolHops`, rate limit, conversation TTL và failover hiện tại.

## 3. Tool contract

| Tool | Arguments do LLM điền | MediatR target | Kết quả |
|---|---|---|---|
| `list_favorites` | Không có | `GetFavoritesQuery(UserId)` | Tối đa 20 sản phẩm yêu thích gần nhất trong prompt |
| `add_favorite` | `marketProductId` | `AddFavoriteCommand(UserId, MarketProductId)` | Sản phẩm đã được thêm; gọi lặp vẫn thành công |
| `remove_favorite` | `marketProductId` | `RemoveFavoriteCommand(UserId, MarketProductId)` | Sản phẩm đã được bỏ; gọi lặp vẫn thành công |
| `get_my_credit` | Không có | `GetRestaurantProfileQuery(UserId)` rồi `GetRestaurantCreditQuery(UserId, false, RestaurantId)` | `creditSummary` trả thẳng frontend; LLM chỉ biết dữ liệu đã sẵn sàng |
| `list_my_orders` | `status?`, `from?`, `to?`, `page?`, `pageSize?` | `ListOrdersQuery(UserId, false, null, ...)` | Danh sách đơn của restaurant hiện tại |
| `list_delivery_addresses` | Không có | `GetDeliveryAddressesQuery(UserId)` | `deliveryAddresses` trả thẳng frontend; LLM chỉ nhận số lượng |

`get_my_credit` là ngoại lệ duy nhất không map 1:1 vì query credit hiện tại cần `RestaurantId`, trong khi assistant chỉ tin `UserId` từ JWT. Tái sử dụng hai query hiện có ngắn hơn và an toàn hơn việc cho model truyền restaurant id hoặc tạo query mới chỉ cho assistant.

### 3.1. Schema chi tiết

`list_favorites`, `get_my_credit`, `list_delivery_addresses`:

```json
{
  "type": "object",
  "properties": {},
  "required": []
}
```

`add_favorite`, `remove_favorite`:

```json
{
  "type": "object",
  "properties": {
    "marketProductId": {
      "type": "string",
      "description": "UUID sản phẩm trong chợ lấy từ kết quả tìm kiếm hoặc danh sách yêu thích."
    }
  },
  "required": ["marketProductId"]
}
```

`list_my_orders`:

```json
{
  "type": "object",
  "properties": {
    "status": {
      "type": "string",
      "description": "draft, confirmed, batched, picked_up, at_hub, delivering, delivered hoặc cancelled."
    },
    "from": { "type": "string", "description": "Thời điểm bắt đầu ISO 8601." },
    "to": { "type": "string", "description": "Thời điểm kết thúc ISO 8601." },
    "page": { "type": "integer", "description": "Trang, mặc định 1." },
    "pageSize": { "type": "integer", "description": "Số đơn mỗi trang, mặc định 10 và tối đa 20." }
  },
  "required": []
}
```

Handler phải từ chối UUID sai, ngày sai, `page < 1` hoặc `pageSize` ngoài `1..20` bằng `INVALID_TOOL_ARGS`; không để exception parse thoát ra ngoài.

## 4. Dữ liệu nhạy cảm không đi qua provider

### 4.1. Response contract

Bổ sung hai field tùy chọn vào `AssistantChatResponse` và `AssistantTurnOutcome`:

```csharp
CreditSummary? CreditSummary = null
IReadOnlyList<AssistantDeliveryAddress>? DeliveryAddresses = null
```

`CreditSummary` chỉ chứa:

- `creditLimit`
- `outstandingBalance`
- `availableCredit`
- `updatedAt`

`AssistantDeliveryAddress` chỉ chứa dữ liệu frontend cần hiển thị/chọn:

- `id`
- `recipientName`
- `phone`
- `addressLine`
- `isDefault`

Không trả `RestaurantId`, latitude hoặc longitude qua assistant response ở phiên bản này.

### 4.2. Orchestrator

Giữ nguyên kiểu trả về `string` của `AssistantTool.Handler`, tránh refactor toàn bộ registry.

Sau khi invoke tool:

1. Với tool bình thường, append `resultJson` như hiện tại.
2. Với `get_my_credit`, parse kết quả thành `CreditSummary`, giữ trong outcome và chỉ append cho LLM:

   ```json
   { "clientDataAvailable": true }
   ```

3. Với `list_delivery_addresses`, parse danh sách thành client payload và chỉ append cho LLM:

   ```json
   { "clientDataAvailable": true, "count": 2 }
   ```

4. Nếu query thất bại, append error envelope hiện tại để LLM giải thích; không tạo client payload.
5. Chỉ lưu JSON đã làm sạch vào `ConversationState`. Không log JSON gốc của hai tool nhạy cảm.

System prompt hướng dẫn model: khi nhận `clientDataAvailable`, thông báo dữ liệu đang được hiển thị cho user và không tự bịa số tiền hoặc địa chỉ.

## 5. Giới hạn dữ liệu đưa vào context

- `list_favorites`: chỉ gửi tối đa 20 item và thêm `totalCount`/`truncated`; frontend vẫn có endpoint favorites để xem toàn bộ khi cần.
- `list_my_orders`: mặc định 10 item, tối đa 20 item mỗi call, sort `createdAt:desc`.
- Không đưa ảnh dạng binary, XML/PDF, credit statement hoặc transaction history vào phạm vi này.

## 6. Thay đổi theo file

### Backend

1. `src/FreshFlow.API/Assistant/Tools/ToolDefinitions.cs`
   - Đăng ký 6 tool.
   - Thêm args record và mapping sang query/command hiện có.
   - Inject identity từ `AssistantToolInvocationContext`.

2. `src/FreshFlow.API/Assistant/AssistantOrchestrator.cs`
   - Thu client payload cho credit/address.
   - Làm sạch tool result trước khi append conversation.

3. `src/FreshFlow.API/Assistant/Dtos/AssistantChatResponse.cs`
   - Thêm DTO và hai field response tùy chọn.

4. `src/FreshFlow.API/Controllers/AssistantController.cs`
   - Map hai field mới từ outcome sang response.

5. `src/FreshFlow.API/Assistant/AssistantSystemPrompt.cs`
   - Mô tả favorites, credit, order history, delivery addresses.
   - Yêu cầu intent rõ ràng trước khi add/remove favorite.
   - Cấm model đoán dữ liệu nhạy cảm.

Không cần migration, config mới, dependency mới hoặc endpoint mới.

### Tests

1. `ToolDefinitionsTests`
   - Registry có đủ 11 tool và tên không trùng.
   - Mỗi tool inject đúng `UserId`; bỏ qua mọi field giả mạo từ LLM.
   - Add/remove favorite parse UUID và map success/failure đúng.
   - Credit resolve profile trước rồi chỉ đọc đúng restaurant của user.
   - List orders luôn `IsAdmin = false`, `RestaurantId = null`, page size không quá 20.
   - Empty-args tool chấp nhận `{}` và không chấp nhận kiểu dữ liệu sai.

2. `AssistantOrchestratorTests`
   - Credit đầy đủ xuất hiện trong outcome nhưng không xuất hiện trong `State.Turns`.
   - Địa chỉ/điện thoại xuất hiện trong outcome nhưng không xuất hiện trong `State.Turns`.
   - Error của credit/address vẫn được gửi lại LLM và không tạo payload.
   - Add/remove favorite đi qua registry mà không bị confirmation gate chặn.

3. `AssistantControllerTests`
   - Response map đúng `creditSummary` và `deliveryAddresses`.
   - Role/rate limit/session ownership không thay đổi.

## 7. Thứ tự triển khai

1. Thêm 4 tool không nhạy cảm: favorites và order history.
2. Thêm DTO response cho credit/address.
3. Thêm `get_my_credit` và `list_delivery_addresses` cùng logic làm sạch trong orchestrator.
4. Cập nhật system prompt.
5. Chạy unit tests Assistant và format gate.
6. Deploy staging và smoke test Gemini thật.

## 8. Smoke test bắt buộc

1. “Cho tôi xem danh sách yêu thích.”
2. Tìm một sản phẩm rồi “Thêm sản phẩm này vào yêu thích.”
3. “Bỏ sản phẩm đó khỏi yêu thích.”
4. “Credit của tôi còn bao nhiêu?” — response có `creditSummary`, log/provider payload không có số dư.
5. “Cho tôi xem 5 đơn đã giao gần nhất.”
6. “Tôi có những địa chỉ giao hàng nào?” — response có địa chỉ, conversation/provider payload không có địa chỉ hoặc điện thoại.
7. Thử prompt chứa `restaurantId`, `userId`, `isAdmin=true`; tất cả phải bị bỏ qua.

## 9. Tiêu chí hoàn thành

- Cả 6 tool hoạt động qua endpoint chat hiện tại.
- Restaurant chỉ đọc/thay đổi dữ liệu của chính mình.
- Add/remove favorite vẫn idempotent.
- Credit, địa chỉ và điện thoại không xuất hiện trong request provider hoặc conversation lưu DB.
- Danh sách lớn không làm context vượt giới hạn đã đặt.
- 5 tool cũ và confirmation flow không đổi behavior.

## 10. Ngoài phạm vi

- Không thêm streaming/WebSocket trong đợt này.
- Không thêm scheduled order, notifications, invoices, claims hoặc credit transactions.
- Không cho assistant sửa địa chỉ, hủy đơn hoặc xác nhận nhận hàng.
- Không tạo confirmation framework tổng quát mới.
- Chưa dynamic-enable tool theo intent; chỉ xem xét nếu smoke test cho thấy 11 tool làm model chọn sai đáng kể.
