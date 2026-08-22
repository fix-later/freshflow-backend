# FreshFlow Backend

Backend ASP.NET Core cho hệ thống FreshFlow. API dùng PostgreSQL để lưu dữ liệu,
Redis cho cache/realtime và tự động chạy EF Core migrations khi khởi động.

## 1. Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (phiên bản được ghim trong `global.json`)
- [Docker](https://docs.docker.com/get-docker/) và Docker Compose
- Git

Kiểm tra sau khi cài:

```bash
dotnet --version
docker --version
docker compose version
```

## 2. Lấy source code

```bash
git clone git@github.com:fix-later/freshflow-backend.git
cd freshflow-backend
```

Tất cả lệnh bên dưới được chạy từ thư mục gốc của repository, trừ khi có ghi
chú khác.

## 3. Khởi động PostgreSQL và Redis

Tạo file `.env` tại thư mục gốc:

```env
POSTGRES_PASSWORD=freshflow_local_pg
REDIS_PASSWORD=freshflow_local_redis
```

`.env` đã được Git ignore. Các giá trị trên chỉ dành cho máy local, không dùng
cho môi trường public.

Khởi động hạ tầng:

```bash
docker compose up -d
docker compose ps
```

Đợi đến khi `postgres` và `redis` đều có trạng thái `healthy`.

| Dịch vụ | Địa chỉ local |
|---|---|
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |

## 4. Cấu hình ứng dụng

Project dùng .NET User Secrets trong môi trường `Development`, nhờ đó mật khẩu
không nằm trong Git.

```bash
cd src/FreshFlow.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=freshflow;Username=ffx;Password=freshflow_local_pg"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379,password=freshflow_local_redis,abortConnect=false"
dotnet user-secrets set "JWT:Key" "freshflow-local-jwt-key-at-least-32-characters"
dotnet user-secrets set "AdminSeed:Password" "FreshFlowAdmin@2026"
dotnet user-secrets set "Cloudinary:CloudName" "local-placeholder"
dotnet user-secrets set "Cloudinary:ApiKey" "local-placeholder"
dotnet user-secrets set "Cloudinary:ApiSecret" "local-placeholder"
cd ../..
```

Chỉ cần chạy `dotnet user-secrets init` một lần. Email admin mặc định là
`admin@freshflow.local` (khai báo trong `appsettings.Development.json`).

Ba giá trị Cloudinary giả ở trên đủ để API khởi động, nhưng upload ảnh sẽ không
hoạt động. Để dùng đầy đủ các tích hợp ngoài, lấy credentials từ người quản lý
dự án rồi cấu hình thêm:

```bash
cd src/FreshFlow.API
dotnet user-secrets set "Cloudinary:CloudName" "<CLOUD_NAME>"
dotnet user-secrets set "Cloudinary:ApiKey" "<API_KEY>"
dotnet user-secrets set "Cloudinary:ApiSecret" "<API_SECRET>"
dotnet user-secrets set "Email:ResendApiKey" "<RESEND_API_KEY>"
dotnet user-secrets set "Delivery:Goong:ApiKey" "<GOONG_API_KEY>"
dotnet user-secrets set "Assistant:ZenMux:ApiKey" "<ZENMUX_API_KEY>"
cd ../..
```

Gemini dùng Google Application Default Credentials. Chỉ cần cấu hình khi dùng
AI Assistant qua Vertex AI:

```bash
gcloud auth application-default login
```

## 5. Chạy API

```bash
dotnet restore FreshFlow.slnx
dotnet run --project src/FreshFlow.API --launch-profile http
```

Lần chạy đầu có thể lâu hơn vì ứng dụng tự động:

- áp dụng EF Core migrations;
- tạo các role hệ thống;
- tạo tài khoản admin mặc định.

Không cần chạy `dotnet ef database update` thủ công.

Sau khi log hiển thị `Now listening on: http://localhost:5110`, kiểm tra:

```bash
curl http://localhost:5110/health
```

Kết quả mong đợi là HTTP `200` với nội dung `Healthy`.

- Swagger UI: <http://localhost:5110/swagger>
- Scalar API Reference: <http://localhost:5110/scalar/v1>
- OpenAPI JSON: <http://localhost:5110/swagger/v1/swagger.json>

Đăng nhập bằng tài khoản admin đã seed:

```bash
curl -X POST http://localhost:5110/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identifier":"admin@freshflow.local","password":"FreshFlowAdmin@2026"}'
```

## 6. Dừng hệ thống

Dừng API bằng `Ctrl+C`, sau đó dừng PostgreSQL và Redis:

```bash
docker compose down
```

Lệnh này giữ lại dữ liệu local. Chỉ dùng lệnh sau khi chắc chắn muốn xóa toàn bộ
database và Redis local:

```bash
docker compose down -v
```

## 7. Chạy test

```bash
dotnet test FreshFlow.slnx
```

Integration tests cần Docker vì Testcontainers sẽ tự tạo database test riêng.

## 8. Lỗi thường gặp

| Lỗi | Cách xử lý |
|---|---|
| `password authentication failed` | Đảm bảo password trong `.env` trùng với connection string trong User Secrets. Nếu Docker volume được tạo bằng password cũ, dùng lại password cũ hoặc xóa volume local bằng `docker compose down -v`. |
| Port `5432` hoặc `6379` đã được dùng | Dừng PostgreSQL/Redis đang chạy trên máy, hoặc đổi cả port mapping trong `docker-compose.yml` và connection string tương ứng. |
| Lỗi validation `JWT`, `Cloudinary` hoặc `AdminSeed` khi startup | Chạy lại các lệnh `dotnet user-secrets set` ở bước 4 trong thư mục `src/FreshFlow.API`. |
| API không kết nối được database | Chạy `docker compose ps`; đợi PostgreSQL `healthy`, rồi khởi động lại API. |
| Upload, email, định tuyến hoặc AI trả lỗi | Thay placeholder bằng credentials thật của Cloudinary, Resend, Goong, ZenMux/GCP. |
| HTTPS certificate warning | Dùng launch profile `http` như hướng dẫn; HTTPS không bắt buộc khi phát triển local. |

## 9. Chạy toàn bộ bằng Docker trên VPS/dev server

Luồng này dành cho máy chủ đã có đầy đủ credentials, không phải cách chạy local
nhanh nhất:

```bash
cp .env.dev-vps.example .env
# Điền toàn bộ giá trị thật trong .env, đặc biệt GCP_SA_KEY_HOST_PATH.
docker compose -f docker-compose.dev-vps.yml up -d --build
docker compose -f docker-compose.dev-vps.yml ps
```

API được bind vào `127.0.0.1:${API_PORT}`; cần reverse proxy nếu muốn truy cập từ
bên ngoài máy chủ. Không commit file `.env` hoặc service-account JSON vào Git.
