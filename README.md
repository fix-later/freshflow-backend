# freshflow-backend
Backend service for the Capstone Project, providing RESTful APIs, business logic, and data management. Built with a scalable architecture and designed for high performance and maintainability.

## Secrets — local development setup

`appsettings.json` and `appsettings.Development.json` contain **no real secrets** — connection string passwords and JWT keys use placeholder values. Before running the API locally, supply real values via **dotnet user-secrets** (stored outside the repo in `~/.microsoft/usersecrets/`):

```bash
cd src/FreshFlow.API

# Initialize user-secrets (once; adds UserSecretsId to the .csproj)
dotnet user-secrets init

# PostgreSQL connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5433;Database=freshflow;Username=ffx;Password=YOUR_PG_PASSWORD"

# Redis connection string (omit password= if Redis has no auth)
dotnet user-secrets set "ConnectionStrings:Redis" \
  "localhost:6379,password=YOUR_REDIS_PASSWORD,abortConnect=false"

# JWT signing key (must be >= 32 characters)
dotnet user-secrets set "JWT:Key" "your-at-least-32-char-local-dev-secret!!"

# Admin seed account password
dotnet user-secrets set "AdminSeed:Password" "YourLocalAdminPassword"

# Cloudinary signed uploads
dotnet user-secrets set "Cloudinary:CloudName" "YOUR_CLOUDINARY_CLOUD_NAME"
dotnet user-secrets set "Cloudinary:ApiKey" "YOUR_CLOUDINARY_API_KEY"
dotnet user-secrets set "Cloudinary:ApiSecret" "YOUR_CLOUDINARY_API_SECRET"
```

User-secrets are loaded automatically in the `Development` environment and override `appsettings.Development.json`.

## Secrets — production / VPS deployment

Secrets are injected as environment variables in `docker-compose.dev-vps.yml`. Copy `.env.dev-vps.example` to `.env` (never commit `.env`) and fill in real values:

```bash
cp .env.dev-vps.example .env
# edit .env — set POSTGRES_PASSWORD, REDIS_PASSWORD, JWT_KEY, CLOUDINARY_*, etc.
docker compose -f docker-compose.dev-vps.yml up -d
```

### Secrets rotation notice

The following secrets were previously committed to git history and **must be rotated** on all deployments:

| Secret | Exposed value |
|--------|---------------|
| PostgreSQL password | `ffx_pgr_251004` |
| Redis password | `redisdev123` |

Generate new passwords, update `.env` on the VPS, and restart the stack.

> **SSH key notice:** `freshflow_cd_ed25519` exists in the working tree but is listed in `.gitignore`. Move it to `~/.ssh/` or a secrets manager to reduce accidental exposure risk.
