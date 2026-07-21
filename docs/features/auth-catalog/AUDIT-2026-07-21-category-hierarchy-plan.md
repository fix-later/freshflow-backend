# Audit / Plan — Category cha/con

## Metadata

| Trường | Giá trị |
|---|---|
| Ngày khảo sát | 2026-07-21 |
| Branch khảo sát | `SCRUM-365-hub-staff-assignment` |
| HEAD khảo sát | `7613782` |
| Task key | Chưa được cấp |
| Trạng thái Jira | ⏳ Chờ Jira key |
| Trạng thái khảo sát | Plan-only; sẵn sàng implement sau khi có Jira key và branch mới |

Không tự đặt Jira key. Không chạm `appsettings.Development.json` hoặc Postman collection đang nằm ngoài scope.

## 1. Phạm vi và quyết định

Category dùng self-reference ngay trên `product_categories`; không tạo bảng hierarchy riêng.

- Thêm `ParentId uuid NULL`, FK tới `product_categories.Id`, index `ParentId`, delete behavior `RESTRICT`.
- `ParentId = null`: category cha. Có `ParentId`: category con.
- Chỉ hỗ trợ đúng hai cấp; không có recursive tree, path hoặc depth column.
- Migration chỉ thêm nullable column/index/FK. Dữ liệu hiện tại giữ nguyên và mặc định là category cha.
- Unique tên Category tiếp tục áp dụng toàn cục như hiện tại.

## 2. Invariants

- Parent phải tồn tại, chưa bị xoá, active và là category cha.
- Không cho self-parent, tạo vòng lặp, tạo cấp thứ ba hoặc chuyển category đang có con thành category con.
- Cho phép tạo root/child, reparent child, promote child bằng `parentId: null`, và demote root chưa có con.
- Khi chỉ đổi tên và giữ nguyên parent, không yêu cầu parent inactive phải active trở lại.
- Chặn deactivate category cha nếu còn category con active; category con inactive không chặn.
- Không cascade deactivate/activate.
- Product tiếp tục được gắn vào category cha hoặc con; validation Category active giữ nguyên.

## 3. Public API

Giữ nguyên endpoint và RBAC:

- `GET /api/v1/categories`
- `GET /api/v1/categories/{id}`
- `POST /api/v1/categories` — admin
- `PUT /api/v1/categories/{id}` — admin
- `PATCH /api/v1/categories/{id}/deactivate` — admin

Create/update nhận thêm `parentId` nullable:

```json
{
  "name": "Rau ăn lá",
  "parentId": "category-parent-guid"
}
```

`parentId: null` tạo hoặc chuyển thành category cha. `CategoryDto` trả thêm `parentId`. GET list vẫn là mảng phẳng, sort theo tên; không thêm `children` hoặc `parentName`.

PUT tiếp tục là full replacement: client cập nhật category con phải gửi lại `parentId`; thiếu trường được hiểu là `null`.

### Error contract

| Code | HTTP | Trường hợp |
|---|---:|---|
| `CATEGORY_PARENT_NOT_FOUND` | 404 | Parent không tồn tại hoặc đã bị xoá |
| `INVALID_CATEGORY_PARENT` | 422 | Parent inactive/là child, self-parent, vòng lặp, cấp ba hoặc demote root còn con |
| `CATEGORY_HAS_ACTIVE_CHILDREN` | 409 | Deactivate parent còn con active |
| `CATEGORY_NAME_CONFLICT` | 409 | Trùng tên toàn cục như hiện tại |

Envelope và RBAC giữ nguyên convention hiện tại.

## 4. Product filtering

Giữ query `GET /api/v1/products?category=...`.

- GUID category cha: trả Product gắn trực tiếp vào cha và Product thuộc các category con của cha.
- GUID category con: chỉ trả Product gắn category con đó.
- GUID không tồn tại: trả trang rỗng.
- Giá trị legacy dạng tên: tiếp tục exact-match `LegacyCategory`; không mở rộng hierarchy.
- Không đổi `ProductDto`, Pricing, Orders hoặc Analytics read models.

Filter được thực hiện trực tiếp trong `ProductRepository` bằng query trên `ProductCategory`; không thêm service hoặc abstraction mới.

## 5. Implementation plan

1. Mở rộng `ProductCategory` với `ParentId` và thao tác đổi parent tối thiểu.
2. Cấu hình self-FK/index và tạo migration `AddProductCategoryHierarchy`.
3. Bổ sung repository query kiểm tra child/active child; tái sử dụng `FindByIdAsync` để validate parent.
4. Mở rộng create/update command, request DTO, mapping và `CategoryDto`.
5. Thêm hierarchy guard vào create/update/deactivate handler trước khi mutate.
6. Mở rộng GUID category filter trong `ProductRepository`.
7. Map hai error code mới sang HTTP convention hiện tại; không tạo tree/move/bulk endpoint.

## 6. Test và verification

### Unit/handler

- Tạo root/child; reject parent missing/inactive/là child.
- Reject self-parent, cấp thứ ba, vòng lặp và demote category còn con.
- Promote, demote và reparent hợp lệ.
- Rename với parent giữ nguyên không revalidate parent inactive.
- Deactivate parent còn con active trả 409; chỉ còn con inactive thì thành công.
- Duplicate name toàn cục giữ nguyên.

### API/integration

- Request/response có `parentId`; list phẳng và RBAC không đổi.
- CRUD root/child, reparent/promote và error envelope/status chuẩn.
- Parent filter trả product trực tiếp + product của con, loại unrelated.
- Child GUID filter exact; GUID không tồn tại trả trang rỗng.
- Legacy string filter giữ behavior cũ.
- Product có thể gắn cả root và child.

### Gates

- Build solution.
- Catalog unit tests và full integration suite.
- `dotnet format FreshFlow.slnx --verify-no-changes`.
- `dotnet ef migrations has-pending-model-changes --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API`.
- `git diff --check`.

## 7. Assumptions và rollout

- Không backfill hoặc suy luận quan hệ từ tên Category.
- Không pagination/tree endpoint, breadcrumb, ordering thủ công hoặc recursive CTE.
- Không tự di chuyển Product khi đổi/deactivate Category.
- Trước commit/rollout: cấp Jira task và tạo branch mới từ `SCRUM-365-hub-staff-assignment` theo Jira key.
- Migration chỉ được tạo trong source; không apply database thật nếu chưa được yêu cầu.

## 8. Nhật ký triển khai local

Theo yêu cầu triển khai trực tiếp ở phiên hiện tại, code và migration `AddProductCategoryHierarchy` đã được tạo local trên branch khảo sát, chưa commit và chưa apply database thật. Jira vẫn chưa được cấp; cần chuyển diff sang branch Jira trước khi commit/rollout.
