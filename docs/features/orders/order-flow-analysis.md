# Order flow review

File sơ đồ: [`order-flow-diagrams.drawio`](./order-flow-diagrams.drawio)

## Cách đọc

File draw.io gồm 8 trang:

1. **Tổng quan**: actor, API, application/domain, persistence, realtime và integration.
2. **Draft và Confirm**: sequence tạo draft, chỉnh item, cutoff 22:00 và charge công nợ.
3. **State machine**: trạng thái domain cho phép và phần application thực sự kích hoạt được.
4. **Công nợ và hủy**: charge, refund, settlement, credit limit và concurrency.
5. **Recurring order**: schedule, hosted job, missed execution và concrete draft instance.
6. **Fulfillment và hậu mãi**: actual quantity, receipt, issue và reorder.
7. **Data model**: các bảng và quan hệ chính của Orders.
8. **Gaps và ưu tiên**: khoảng trống được xếp theo tác động tới luồng end-to-end.

Quy ước màu: xanh là phần đã implement; vàng là rule/check hoặc phần phụ thuộc; đỏ nét đứt là phần thiếu hoặc chưa được nối.

## Kết luận chính

- Luồng chạy được hiện tại là `Draft -> Confirmed` và `Draft/Confirmed -> Cancelled`.
- Domain định nghĩa pipeline đến `Delivered`, nhưng không có command/API/consumer gọi `Order.AdvanceStatus()`.
- Draft kiểm tra tồn kho lúc tạo/sửa, nhưng không reserve và confirm không kiểm tra/deduct lại tồn kho.
- Recurring job tạo draft rỗng vì `ScheduledOrder` không lưu line-item template.
- Công nợ được charge/refund atomically cùng order, nhưng settlement không phân bổ theo order nên `OrderPaymentStatus.Settled` chưa được sử dụng.
- Confirmed/cancelled integration events đã được publish sau commit nhưng chưa có consumer trong repo.
- `OrderIssue.Resolve()` có trong entity nhưng chưa có application/API workflow.
- `PreviewOrderConfirmationQueryHandler` đã có nhưng chưa được expose qua controller, nên client không gọi được luồng preview.

## Code đã đối chiếu

- `src/FreshFlow.API/Controllers/OrdersController.cs`
- `src/FreshFlow.API/Controllers/AdminController.cs`
- `src/FreshFlow.API/Controllers/RestaurantCreditController.cs`
- `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Commands/`
- `src/Modules/Orders/FreshFlow.Orders.Application/Queries/`
- `src/Modules/Orders/FreshFlow.Orders.Application/Services/`
- `src/Modules/Orders/FreshFlow.Orders.Infrastructure/`
- `src/FreshFlow.Infrastructure.Persistence/DomainEventDispatchInterceptor.cs`
