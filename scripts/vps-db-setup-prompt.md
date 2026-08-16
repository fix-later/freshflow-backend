# Prompt: FreshFlow Base Database Setup (Docker Compose)

Copy toàn bộ nội dung phần **"## Prompt"** bên dưới và paste vào Claude trên VPS.

---

## Prompt

You are setting up the base database infrastructure for the **FreshFlow** backend project on this VPS using Docker Compose.

**Goal:** Deploy PostgreSQL 16 and Redis 7 as Docker containers so the .NET application can connect to them. Do NOT create any application tables — EF Core migrations will handle that later.

**Constraints:**
- Use Docker Compose (not bare `docker run`)
- Both containers must bind ports to loopback only (`127.0.0.1`) — never expose to the public internet
- Generate strong random passwords with `openssl rand -base64 24`
- Persist data via named Docker volumes so data survives container restarts
- Save all credentials to `/root/.freshflow_db_credentials` (chmod 600)

Work through the steps below one at a time. After each bash command, wait for the output before continuing.

---

### Step 1 — Check prerequisites

```bash
docker --version 2>/dev/null || echo "Docker NOT installed"
docker compose version 2>/dev/null || echo "Docker Compose plugin NOT installed"
openssl version
```

If Docker is not installed, install it now:

```bash
curl -fsSL https://get.docker.com | bash
systemctl enable docker
systemctl start docker
```

Verify:
```bash
docker --version
docker compose version
```

---

### Step 2 — Create project directory

```bash
mkdir -p /opt/freshflow/infra
cd /opt/freshflow/infra
```

---

### Step 3 — Generate credentials

```bash
DB_PASS=$(openssl rand -base64 24)
REDIS_PASS=$(openssl rand -base64 24)
echo "DB_PASS=$DB_PASS"
echo "REDIS_PASS=$REDIS_PASS"
```

Store them in your shell for the next steps:
```bash
export DB_PASS REDIS_PASS
```

---

### Step 4 — Write the .env file

```bash
cat > /opt/freshflow/infra/.env <<EOF
POSTGRES_DB=freshflow
POSTGRES_USER=freshflow_app
POSTGRES_PASSWORD=${DB_PASS}
REDIS_PASSWORD=${REDIS_PASS}
EOF
chmod 600 /opt/freshflow/infra/.env
```

---

### Step 5 — Write docker-compose.yml

```bash
cat > /opt/freshflow/infra/docker-compose.yml <<'EOF'
services:
  postgres:
    image: postgres:16-alpine
    container_name: freshflow-postgres
    restart: unless-stopped
    env_file: .env
    environment:
      POSTGRES_INITDB_ARGS: "--encoding=UTF8 --locale=C"
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-postgres.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U freshflow_app -d freshflow"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: freshflow-redis
    restart: unless-stopped
    command: >
      redis-server
      --requirepass ${REDIS_PASSWORD}
      --appendonly yes
      --maxmemory-policy allkeys-lru
      --bind 0.0.0.0
      --protected-mode no
    ports:
      - "127.0.0.1:6379:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "PING"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
  redis_data:
EOF
```

---

### Step 6 — Write PostgreSQL init script

This enables the required extensions on first boot:

```bash
cat > /opt/freshflow/infra/init-postgres.sql <<'EOF'
-- Extensions required by FreshFlow
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
EOF
```

---

### Step 7 — Start containers

```bash
cd /opt/freshflow/infra
docker compose up -d
```

Wait ~15 seconds, then check status:

```bash
docker compose ps
docker compose logs postgres --tail 20
docker compose logs redis --tail 20
```

Both containers must show status `healthy` (or `running`) before continuing.

---

### Step 8 — Smoke tests

**PostgreSQL:**
```bash
docker exec freshflow-postgres psql \
  -U freshflow_app -d freshflow \
  -c "SELECT current_database(), current_user, gen_random_uuid();"
```

**PostgreSQL extensions:**
```bash
docker exec freshflow-postgres psql \
  -U freshflow_app -d freshflow \
  -c "\dx"
```

**Redis:**
```bash
docker exec freshflow-redis redis-cli \
  -a "$REDIS_PASS" PING

docker exec freshflow-redis redis-cli \
  -a "$REDIS_PASS" SET freshflow:test ok EX 30

docker exec freshflow-redis redis-cli \
  -a "$REDIS_PASS" GET freshflow:test
```

All tests must pass before saving credentials.

---

### Step 9 — Save credentials file

```bash
cat > /root/.freshflow_db_credentials <<EOF
# FreshFlow DB credentials — generated $(date -u +"%Y-%m-%dT%H:%M:%SZ")
# Docker Compose project: /opt/freshflow/infra

FRESHFLOW_DB_NAME=freshflow
FRESHFLOW_DB_USER=freshflow_app
FRESHFLOW_DB_PASS=${DB_PASS}
FRESHFLOW_DB_PORT=5432

FRESHFLOW_REDIS_PASS=${REDIS_PASS}
FRESHFLOW_REDIS_PORT=6379

# .NET connection strings — copy into appsettings.Production.json:
#
# "ConnectionStrings": {
#   "Postgres": "Host=127.0.0.1;Port=5432;Database=freshflow;Username=freshflow_app;Password=${DB_PASS};Pooling=true;MinPoolSize=5;MaxPoolSize=100;CommandTimeout=30",
#   "Redis": "127.0.0.1:6379,password=${REDIS_PASS},abortConnect=false"
# }
#
# Useful commands:
#   docker compose -f /opt/freshflow/infra/docker-compose.yml up -d    # start
#   docker compose -f /opt/freshflow/infra/docker-compose.yml down      # stop (data preserved)
#   docker compose -f /opt/freshflow/infra/docker-compose.yml down -v   # stop + wipe data
EOF
chmod 600 /root/.freshflow_db_credentials
```

---

### Step 10 — Report results

Output a summary containing:
1. Docker and Docker Compose versions
2. Container status (`docker compose ps` output)
3. Whether all smoke tests passed (PostgreSQL query result + Redis PONG)
4. Full contents of `/root/.freshflow_db_credentials` so I can copy the connection strings

---

**Rules for Claude on VPS:**
- Run all commands as root
- If any step fails, stop immediately and report the full error output — do not continue
- Do not skip smoke tests
- The `.env` file holds the source of truth for passwords; never hardcode them elsewhere
- Application tables will be created later via `dotnet ef database update` on the app server
