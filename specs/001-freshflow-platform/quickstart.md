# Quickstart: FreshFlow Platform

**Branch**: `001-freshflow-platform` | **Date**: 2026-05-11
**Goal**: Full stack running locally in under 10 minutes for a new team member.

---

## Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| .NET SDK | 8.x | https://dotnet.microsoft.com/download |
| Docker Desktop | 4.x | https://www.docker.com/products/docker-desktop |
| Node.js | 20 LTS | https://nodejs.org |
| Git | any | |

Optional but recommended:
- **Rider** or **VS Code** with C# Dev Kit extension
- **TablePlus** or **psql** for direct PostgreSQL access
- **Redis Insight** for Redis inspection

---

## Step 1: Clone and configure environment

```bash
git clone <repo-url> freshflow-backend
cd freshflow-backend

# Copy the example env file and fill in secrets
cp .env.example .env
```

Edit `.env` — the defaults work for local development, but set a real JWT secret:

```env
POSTGRES_DB=freshflow
POSTGRES_USER=ffx
POSTGRES_PASSWORD=localdev123

REDIS_PASSWORD=redisdev123

ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=freshflow;Username=ffx;Password=localdev123
ConnectionStrings__Redis=redis:6379,password=redisdev123,abortConnect=false

Jwt__SecretKey=change-me-to-a-real-32-byte-secret-key!!
Jwt__AccessTokenTTL=900
Jwt__RefreshTokenTTL=604800

SignalR__UseRedis=true

SEED_ADMIN_EMAIL=admin@freshflow.vn
SEED_ADMIN_PASSWORD=Admin@2026!
```

---

## Step 2: Start the infrastructure containers

```bash
docker compose up -d postgres redis
```

Wait ~10 seconds for PostgreSQL to become healthy:

```bash
docker compose ps   # postgres and redis should show "healthy"
```

---

## Step 3: Build and run the API

```bash
# Build
dotnet build FreshFlow.sln

# Run (migrations apply automatically on first start)
dotnet run --project src/FreshFlow.API

# Verify
curl http://localhost:5000/health
# Expected: {"status":"Healthy",...}
```

The API is now available at `http://localhost:5000`. Swagger UI is at
`http://localhost:5000/swagger` (Development environment only).

---

## Step 4 (optional): Run with full Docker Compose

To run the complete stack (nginx + api + postgres + redis):

```bash
docker compose up -d
```

- API via Nginx: `http://localhost` (port 80)
- Direct API (without Nginx): `http://localhost:8080`
- PostgreSQL: `localhost:5432` (dev port exposed)
- Redis: `localhost:6379` (dev port exposed)

---

## Step 5: Seed data verification

On first run, the `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` environment variables
create a bootstrapped Admin account. Verify:

```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@freshflow.vn","password":"Admin@2026!"}'
```

Expected: HTTP 200 with `accessToken` in the response body.

---

## Step 6: Run tests

```bash
# Unit tests only (no Docker required)
dotnet test FreshFlow.sln --filter "Category=Unit"

# Integration tests (requires Docker — containers spun up automatically by Testcontainers)
dotnet test FreshFlow.sln --filter "Category=Integration"

# All tests
dotnet test FreshFlow.sln
```

---

## Step 7 (optional): Angular frontend

```bash
cd freshflow-web
npm install
npm start         # Starts Angular dev server at http://localhost:4200
```

The Angular app proxies API calls to `http://localhost:5000` in development mode.

---

## Step 8 (optional): React Native mobile

```bash
cd freshflow-mobile
npm install
npx expo start    # Or: npx react-native start
```

For physical device testing, update `freshflow-mobile/src/services/api.service.ts`
to point to your machine's LAN IP (e.g., `http://192.168.1.x:5000`).

---

## Common Operations

### Add an EF Core migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API
```

### Apply pending migrations manually

```bash
dotnet ef database update \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API
```

### Check code formatting (CI gate)

```bash
dotnet format FreshFlow.sln --verify-no-changes
```

### Wipe the local database

```bash
docker compose down -v   # -v removes named volumes including pgdata
docker compose up -d postgres redis
# Migrations will re-apply on next dotnet run
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `GET /health` returns 503 | PostgreSQL not ready | `docker compose ps` — wait for healthy status |
| `GET /health` returns Degraded | Redis unavailable | Check Redis container; API functions without Redis (degraded mode) |
| Migration fails on first run | Database user lacks CREATE TABLE | Ensure `POSTGRES_USER` has superuser rights (default in Docker Compose) |
| `401 Unauthorized` on all requests | JWT secret mismatch | Ensure `Jwt__SecretKey` in `.env` matches running API |
| SignalR connect fails | Redis backplane misconfigured | Set `SignalR__UseRedis=false` in `.env` for single-instance local dev |
| Port 5432 in use | Local PostgreSQL running | Stop the local service or change `docker-compose.yml` port mapping |

---

## Environment Variable Reference

See `src/FreshFlow.API/appsettings.json` for all configurable values.
See `docs/02-system-architecture.md` Section 7.3 for the complete variable table.
