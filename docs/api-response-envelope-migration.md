# API Response Envelope — Migration Guide

> **Áp dụng cho:** FreshFlow Mobile App (React Native) và Web (React)  
> **Phiên bản API:** v1  
> **Ngày hiệu lực:** 2026-06-10 (commit `d4dcf7b`)

---

## Tóm tắt thay đổi

Tất cả response từ API giờ được bọc trong một **envelope chuẩn**. Trước đây một số endpoint trả raw object hoặc body rỗng — điều này đã được chuẩn hoá.

| | Trước | Sau |
|---|---|---|
| Success có data | Raw object `{ id, email, ... }` | `{ "success": true, "data": { id, email, ... } }` |
| Success không có data | HTTP 200/202 body rỗng | `{ "success": true, "data": null }` |
| Lỗi | `{ "success": false, "error": { ... } }` | Không đổi |

---

## Format chuẩn

### 1. Success — có data

```json
{
  "success": true,
  "data": { ... }
}
```

### 2. Success — không có data (action, void)

```json
{
  "success": true,
  "data": null
}
```

### 3. Lỗi

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Mô tả lỗi thân thiện với người dùng"
  }
}
```

### 4. Danh sách có phân trang

Data là object có 2 key: `data` (mảng items) và `meta` (thông tin trang).

```json
{
  "success": true,
  "data": {
    "data": [ { ... }, { ... } ],
    "meta": {
      "total": 120,
      "page": 1,
      "pageSize": 20
    }
  }
}
```

---

## HTTP Status Codes

| Code | Ý nghĩa |
|------|---------|
| `200 OK` | Thành công, có data |
| `201 Created` | Tạo mới thành công |
| `202 Accepted` | Yêu cầu đã nhận (vd: gửi email, verify OTP) |
| `400 Bad Request` | Validation lỗi, dữ liệu không hợp lệ |
| `401 Unauthorized` | Chưa đăng nhập hoặc token hết hạn |
| `403 Forbidden` | Không có quyền |
| `404 Not Found` | Không tìm thấy resource |
| `409 Conflict` | Duplicate (email/phone đã tồn tại, token bị reuse) |
| `422 Unprocessable Entity` | Dữ liệu hợp lệ về cú pháp nhưng không xử lý được (vd: market không hợp lệ) |
| `423 Locked` | Tài khoản bị khoá |
| `429 Too Many Requests` | Rate limit (auth endpoints: 10 req/phút/IP) |
| `500 Internal Server Error` | Lỗi server |

---

## Error Codes

### Auth

| Code | HTTP | Ý nghĩa |
|------|------|---------|
| `INVALID_CREDENTIALS` | 401 | Sai email/mật khẩu |
| `ACCOUNT_LOCKED` | 423 | Tài khoản bị khoá tạm thời |
| `ACCOUNT_INACTIVE` | 422 | Tài khoản bị vô hiệu hoá |
| `UNAUTHORIZED` | 401 | Token thiếu hoặc không hợp lệ |
| `TOKEN_EXPIRED` | 401 | Access token hết hạn (xem note bên dưới) |
| `TOKEN_INVALID` | 401 | Token bị giả mạo/corrupt |
| `REFRESH_TOKEN_EXPIRED` | 401 | Refresh token hết hạn, yêu cầu đăng nhập lại |
| `REFRESH_TOKEN_REVOKED` | 401 | Refresh token đã bị thu hồi |
| `REFRESH_TOKEN_REUSE` | 409 | Phát hiện reuse attack, toàn bộ session bị revoke |
| `FORBIDDEN` | 403 | Không có role phù hợp |

> **TOKEN_EXPIRED:** Khi access token hết hạn, middleware trả về 401 với body `{ "code": "TOKEN_EXPIRED", "message": "..." }` — **không** có wrapper `success/error`, đây là response trực tiếp từ JWT middleware. Xử lý bằng cách gọi `POST /api/v1/auth/refresh`.

### Đăng ký / Profile

| Code | HTTP | Ý nghĩa |
|------|------|---------|
| `EMAIL_ALREADY_EXISTS` | 409 | Email đã được đăng ký |
| `PHONE_ALREADY_EXISTS` | 409 | Số điện thoại đã được đăng ký |
| `WEAK_PASSWORD` | 400 | Mật khẩu không đủ mạnh |
| `INVALID_CURRENT_PASSWORD` | 401 | Mật khẩu hiện tại sai |
| `VALIDATION_ERROR` | 400 | Dữ liệu gửi lên không hợp lệ (xem `errors` array nếu có) |

### Reset mật khẩu / OTP

| Code | HTTP | Ý nghĩa |
|------|------|---------|
| `RESET_TOKEN_INVALID` | 400 | Token reset không hợp lệ |
| `RESET_TOKEN_EXPIRED` | 400 | Token reset hết hạn |
| `OTP_INVALID` | 400 | Mã OTP sai hoặc hết hạn |
| `CHANNEL_NOT_SUPPORTED` | 422 | Kênh xác thực không hỗ trợ |

### Catalog

| Code | HTTP | Ý nghĩa |
|------|------|---------|
| `PRODUCT_NOT_FOUND` | 404 | Không tìm thấy sản phẩm |
| `CATEGORY_NOT_FOUND` | 404 | Không tìm thấy danh mục |
| `UNIT_NOT_FOUND` | 404 | Không tìm thấy đơn vị |
| `CATEGORY_NAME_CONFLICT` | 409 | Tên danh mục đã tồn tại |
| `UNIT_NAME_CONFLICT` | 409 | Tên đơn vị đã tồn tại |
| `INVALID_CATEGORY` | 422 | CategoryId không tồn tại |
| `INVALID_UNIT` | 422 | UnitId không tồn tại |

### Rate Limit

| Code | HTTP | Ý nghĩa |
|------|------|---------|
| `TOO_MANY_REQUESTS` | 429 | Quá nhiều request (auth endpoints: 10/phút/IP) |

---

## Ví dụ cụ thể

### POST /api/v1/auth/register — thành công

```http
HTTP/1.1 201 Created
```
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "owner@pho-ha-noi.vn",
    "role": "restaurant"
  }
}
```

### POST /api/v1/auth/login — thành công

```http
HTTP/1.1 200 OK
```
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJ...",
    "refreshToken": "abc123...",
    "expiresIn": 1800,
    "user": {
      "id": "3fa85f64-...",
      "email": "owner@pho-ha-noi.vn",
      "role": "restaurant",
      "approvalStatus": "approved"
    }
  }
}
```

### POST /api/v1/auth/forgot-password — thành công (luôn 202, dù email có tồn tại hay không)

```http
HTTP/1.1 202 Accepted
```
```json
{
  "success": true,
  "data": null
}
```

### GET /api/v1/products?page=1&pageSize=20 — danh sách có phân trang

```http
HTTP/1.1 200 OK
```
```json
{
  "success": true,
  "data": {
    "data": [
      {
        "id": "...",
        "name": "Cá trắm",
        "categoryId": "...",
        "categoryName": "Thủy hải sản",
        "unitId": "...",
        "unitName": "kg",
        "description": null,
        "createdBy": "...",
        "createdAt": "2026-06-01T08:00:00Z",
        "updatedAt": "2026-06-01T08:00:00Z",
        "isDeleted": false
      }
    ],
    "meta": {
      "total": 84,
      "page": 1,
      "pageSize": 20
    }
  }
}
```

### Lỗi validation

```http
HTTP/1.1 400 Bad Request
```
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "'Email' must not be empty."
  }
}
```

### Lỗi rate limit

```http
HTTP/1.1 429 Too Many Requests
```
```json
{
  "success": false,
  "error": {
    "code": "TOO_MANY_REQUESTS",
    "message": "Too many requests. Please slow down and try again shortly."
  }
}
```

---

## Hướng dẫn migration code

### TypeScript — kiểu dữ liệu chuẩn

```typescript
// types/api.ts

export interface ApiSuccess<T> {
  success: true;
  data: T;
}

export interface ApiError {
  success: false;
  error: {
    code: string;
    message: string;
  };
}

export type ApiResponse<T> = ApiSuccess<T> | ApiError;

export interface PaginationMeta {
  total: number;
  page: number;
  pageSize: number;
}

export interface PaginatedData<T> {
  data: T[];
  meta: PaginationMeta;
}
```

### Trước (cũ) — raw response

```typescript
// ❌ Cũ: response trả thẳng object
const res = await fetch('/api/v1/auth/login', { ... });
const user = await res.json(); // { id, email, role, ... }
```

### Sau (mới) — unwrap envelope

```typescript
// ✅ Mới: unwrap envelope trước khi dùng
const res = await fetch('/api/v1/auth/login', { ... });
const body: ApiResponse<LoginData> = await res.json();

if (!body.success) {
  // xử lý lỗi
  handleApiError(body.error.code, body.error.message);
  return;
}

const { accessToken, user } = body.data; // an toàn
```

### Utility function cho axios/fetch

```typescript
// utils/apiClient.ts

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, options);
  const body: ApiResponse<T> = await res.json();

  if (!body.success) {
    throw new ApiException(body.error.code, body.error.message, res.status);
  }

  return body.data;
}

class ApiException extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly httpStatus: number
  ) {
    super(message);
    this.name = 'ApiException';
  }
}
```

### Xử lý token hết hạn (TOKEN_EXPIRED)

JWT middleware trả 401 với body **không có** wrapper `success/error` — format:

```json
{ "code": "TOKEN_EXPIRED", "message": "The access token has expired." }
```

Interceptor nên handle:

```typescript
// Kiểm tra cả res.status === 401 lẫn body.code
if (res.status === 401) {
  const body = await res.json();
  if (body.code === 'TOKEN_EXPIRED' || body?.error?.code === 'TOKEN_EXPIRED') {
    await refreshAccessToken();
    return retry(originalRequest);
  }
}
```

### Paginated list

```typescript
// Với endpoint GET /api/v1/products
const result = await apiFetch<PaginatedData<ProductDto>>('/api/v1/products?page=1&pageSize=20');
// result.data   → ProductDto[]
// result.meta   → { total, page, pageSize }
```

---

## Checklist migration

- [ ] Tất cả response handler đã unwrap `body.data` thay vì dùng trực tiếp `body`
- [ ] Handler lỗi kiểm tra `body.success === false` → đọc `body.error.code`
- [ ] 202 response (forgot-password, verify, revoke) đã xử lý `data: null`
- [ ] Paginated list đọc `body.data.data` (items) và `body.data.meta` (pagination)
- [ ] Token refresh interceptor handle cả JWT middleware 401 format và envelope format
- [ ] Rate limit 429 hiển thị thông báo phù hợp với user
