# Quickstart: FreshFlow Auth Module v1

**Date**: 2026-05-31

---

## Prerequisites

- .NET 10 SDK
- Docker Desktop (for PostgreSQL via `docker compose`)
- Access to `.env` file (see `.env.example`)

---

## 1. Start Local Infrastructure

```bash
docker compose up -d postgres redis
```

Verify PostgreSQL is accepting connections:
```bash
docker compose exec postgres pg_isready
```

---

## 2. Configure Secrets

Copy `.env.example` to `.env` and fill in:

```env
# JWT
JWT__Key=<256-bit random secret — generate with: openssl rand -base64 32>
JWT__Issuer=https://api.freshflow.vn
JWT__Audience=freshflow-api

# Admin seed
ADMIN_SEED_EMAIL=admin@freshflow.vn
ADMIN_SEED_PASSWORD=<min 8 chars, 1 upper, 1 digit, 1 special>

# Database
DB_CONNECTION_STRING=Host=localhost;Port=5432;Database=freshflow;Username=freshflow;Password=freshflow
```

---

## 3. Apply Migrations

```bash
dotnet ef database update \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API
```

The Auth migration creates: `users`, `refresh_tokens`, `user_market_assignments`, `driver_profiles` tables.

On first startup, `AdminSeeder` (hosted service) runs and creates the seeded Admin account from `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD`. Check logs for `[AdminSeeder] Admin account seeded` or `[AdminSeeder] Admin account already exists, skipping`.

---

## 4. Run the API

```bash
dotnet run --project src/FreshFlow.API
```

API listens at `https://localhost:7001`.

---

## 5. Smoke Test Auth Flow

### Log in as Admin

```bash
curl -s -X POST https://localhost:7001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@freshflow.vn","password":"<ADMIN_SEED_PASSWORD>"}' | jq .
```

Expected: `200 OK` with `accessToken`, `refreshToken`, `expiresIn: 900`.

### Create a Restaurant user

```bash
ACCESS_TOKEN="<accessToken from above>"

curl -s -X POST https://localhost:7001/api/v1/admin/users \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"email":"manager@phobaatu.vn","password":"TempP@ss1","role":"restaurant","restaurantName":"Phở Bà Tu"}' | jq .
```

Expected: `201 Created` with new user ID.

### Approve the Restaurant

```bash
curl -s -X PATCH "https://localhost:7001/api/v1/admin/restaurants/<restaurantId>/approve" \
  -H "Authorization: Bearer $ACCESS_TOKEN" | jq .
```

Expected: `200 OK` with `isApproved: true`.

### Refresh the token

```bash
curl -s -X POST https://localhost:7001/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken from login>"}' | jq .
```

Expected: `200 OK` with new token pair. The old `refreshToken` is now invalid.

### Logout

```bash
curl -s -X POST https://localhost:7001/api/v1/auth/logout \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<new refreshToken>"}' -o /dev/null -w "%{http_code}"
```

Expected: `204`.

---

## 6. Run Tests

### Unit tests only (no Docker needed)

```bash
dotnet test tests/Unit/FreshFlow.Auth.UnitTests/ --filter Category=Unit
```

### Integration tests (requires Docker Compose PostgreSQL running)

```bash
dotnet test tests/Integration/FreshFlow.IntegrationTests/ --filter Category=Integration
```

Integration tests use `Testcontainers.PostgreSql` — they spin up their own container automatically.

### All four CI gates

```bash
# 1. Unit tests
dotnet test FreshFlow.sln --filter Category=Unit

# 2. Integration tests
dotnet test FreshFlow.sln --filter Category=Integration

# 3. Format check
dotnet format FreshFlow.sln --verify-no-changes

# 4. Security scan (run manually or via CI)
dotnet tool run dotnet-ossindex
```

---

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `AdminSeeder` fails on startup | DB unreachable | Ensure `docker compose up postgres` is running |
| 401 on all requests | JWT key mismatch | Check `JWT__Key` in `.env` matches what the API loaded |
| 409 on repeated seed runs | Seed already applied | Normal — seeder skips if Admin exists |
| `ACCOUNT_PENDING_APPROVAL` on restaurant login | Restaurant not approved | Call `PATCH /admin/restaurants/{id}/approve` |
