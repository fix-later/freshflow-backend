# FreshFlow Dev VPS Access Guide

**Scope:** Huong dan teammate truy cap backend API, PostgreSQL, Redis va RedisInsight tren dev VPS thong qua SSH tunnel.

Dev VPS hien bind cac service vao `127.0.0.1` tren server, khong public truc tiep PostgreSQL/Redis ra internet. Vi vay may local phai ket noi qua SSH tunnel.

---

## 1. Service Ports

| Service | Local URL sau khi mo tunnel | VPS target |
|---|---|---|
| Backend API | `http://localhost:8080` | `127.0.0.1:8080` |
| PostgreSQL | `localhost:5433` | `127.0.0.1:5433` |
| Redis | `localhost:6379` | `127.0.0.1:6379` |
| RedisInsight | `http://localhost:5540` | `127.0.0.1:5540` |

Credentials lay tu nguoi quan tri VPS:

```text
VPS_HOST=<dev-vps-ip>
VPS_USER=<ssh-user>
POSTGRES_DB=freshflow
POSTGRES_USER=ffx
POSTGRES_PASSWORD=<from .env.dev-vps>
REDIS_PASSWORD=<from .env.dev-vps>
```

Khong commit `.env.dev-vps`, password, private key, hoac file key ca nhan vao repo.

---

## 2. One-Time SSH Access Setup

Moi teammate nen co SSH key rieng. Khong dung chung private key.

### Linux/macOS

Kiem tra da co key chua:

```bash
ls -la ~/.ssh
```

Neu chua co key phu hop:

```bash
ssh-keygen -t ed25519 -C "your-name@freshflow" -f ~/.ssh/id_ed25519_freshflow_dev
```

In public key de gui cho nguoi quan tri VPS:

```bash
cat ~/.ssh/id_ed25519_freshflow_dev.pub
```

Them SSH config:

```sshconfig
Host freshflow-dev-vps
    HostName <VPS_HOST>
    User <VPS_USER>
    IdentityFile ~/.ssh/id_ed25519_freshflow_dev
    ExitOnForwardFailure yes
    ServerAliveInterval 60
    LocalForward 8080 127.0.0.1:8080
    LocalForward 5433 127.0.0.1:5433
    LocalForward 6379 127.0.0.1:6379
    LocalForward 5540 127.0.0.1:5540
```

### Windows PowerShell

Kiem tra da co key chua:

```powershell
dir $env:USERPROFILE\.ssh
```

Neu chua co key phu hop:

```powershell
ssh-keygen -t ed25519 -C "your-name@freshflow" -f $env:USERPROFILE\.ssh\id_ed25519_freshflow_dev
```

In public key de gui cho nguoi quan tri VPS:

```powershell
type $env:USERPROFILE\.ssh\id_ed25519_freshflow_dev.pub
```

Tao hoac sua file:

```text
C:\Users\<YourUser>\.ssh\config
```

Noi dung:

```sshconfig
Host freshflow-dev-vps
    HostName <VPS_HOST>
    User <VPS_USER>
    IdentityFile ~/.ssh/id_ed25519_freshflow_dev
    ExitOnForwardFailure yes
    ServerAliveInterval 60
    LocalForward 8080 127.0.0.1:8080
    LocalForward 5433 127.0.0.1:5433
    LocalForward 6379 127.0.0.1:6379
    LocalForward 5540 127.0.0.1:5540
```

### Admin: Add Teammate Public Key To VPS

Tren VPS, nguoi quan tri them public key cua teammate vao `authorized_keys`:

```bash
mkdir -p ~/.ssh
chmod 700 ~/.ssh
printf '%s\n' '<PASTE_TEAMMATE_PUBLIC_KEY>' >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

---

## 3. Open Tunnel

### Linux/macOS

Chay foreground, giu terminal mo:

```bash
ssh freshflow-dev-vps
```

Hoac chay background:

```bash
ssh -fN freshflow-dev-vps
```

Dung tunnel background:

```bash
pkill -f "ssh -fN freshflow-dev-vps"
```

### Windows PowerShell

Chay foreground, giu terminal mo:

```powershell
ssh freshflow-dev-vps
```

Hoac chay background:

```powershell
Start-Process ssh -ArgumentList "-N freshflow-dev-vps" -WindowStyle Hidden
```

Dung tat ca process SSH dang chay:

```powershell
Get-Process ssh | Stop-Process
```

Neu dang co nhieu SSH session khac, tat dung process trong Task Manager thay vi command tren.

### PuTTY Option For Windows

1. Mo PuTTY.
2. `Host Name`: `<VPS_HOST>`.
3. `Port`: `22`.
4. Vao `Connection > SSH > Tunnels`.
5. Them cac tunnel:

```text
Source port: 8080
Destination: 127.0.0.1:8080
Type: Local
```

```text
Source port: 5433
Destination: 127.0.0.1:5433
Type: Local
```

```text
Source port: 6379
Destination: 127.0.0.1:6379
Type: Local
```

```text
Source port: 5540
Destination: 127.0.0.1:5540
Type: Local
```

6. Bam `Open` va login. Giu PuTTY mo khi dang lam viec.

---

## 4. Validate Connection

### Backend API

```bash
curl http://localhost:8080/health
```

Expected:

```text
Healthy
```

Mo API tu browser:

```text
http://localhost:8080
```

### PostgreSQL

DBeaver, DataGrip, pgAdmin:

```text
Host: localhost
Port: 5433
Database: freshflow
Username: ffx
Password: <POSTGRES_PASSWORD>
```

Terminal:

```bash
psql "host=localhost port=5433 dbname=freshflow user=ffx password=<POSTGRES_PASSWORD>"
```

### Redis

```bash
redis-cli -h localhost -p 6379 -a '<REDIS_PASSWORD>' ping
```

Expected:

```text
PONG
```

### RedisInsight

Mo browser:

```text
http://localhost:5540
```

Add Redis database:

```text
Host: 127.0.0.1
Port: 6379
Username: <empty>
Password: <REDIS_PASSWORD>
```

---

## 5. Run Backend Locally Against VPS DB/Redis

Mo tunnel truoc, sau do chay backend local voi connection string tro ve localhost.

### Linux/macOS

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5433;Database=freshflow;Username=ffx;Password=<POSTGRES_PASSWORD>'
export ConnectionStrings__Redis='localhost:6379,password=<REDIS_PASSWORD>,abortConnect=false'

dotnet run --project src/FreshFlow.API/FreshFlow.API.csproj
```

### Windows PowerShell

```powershell
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=freshflow;Username=ffx;Password=<POSTGRES_PASSWORD>"
$env:ConnectionStrings__Redis="localhost:6379,password=<REDIS_PASSWORD>,abortConnect=false"

dotnet run --project src/FreshFlow.API/FreshFlow.API.csproj
```

---

## 6. Common Issues

### `Permission denied (publickey)`

Public key cua teammate chua duoc add vao VPS, hoac SSH config dang tro sai `IdentityFile`.

Kiem tra key dang dung:

```bash
ssh -v freshflow-dev-vps
```

### `bind: Address already in use`

May local dang co process dung port do. Doi local port trong `LocalForward`, vi du:

```sshconfig
LocalForward 15433 127.0.0.1:5433
```

Khi do PostgreSQL local connection se la:

```text
Host: localhost
Port: 15433
```

### `curl http://localhost:8080/health` fail

Kiem tra tunnel co chay khong:

```bash
ssh -O check freshflow-dev-vps
```

Neu command tren khong dung duoc do tunnel khong mo control socket, kiem tra port:

```bash
ss -ltnp | grep 8080
```

Tren Windows PowerShell:

```powershell
netstat -ano | findstr :8080
```

Sau do kiem tra container tren VPS:

```bash
docker compose --env-file .env.dev-vps -f docker-compose.dev-vps.yml ps
docker compose --env-file .env.dev-vps -f docker-compose.dev-vps.yml logs -f api
```

### Shared Dev DB Conflict

Tat ca teammate dang dung chung DB `freshflow`. Khong reset, truncate, hoac migrate bat thuong khi nguoi khac dang test. Neu can tach data, tao DB rieng cho tung nguoi:

```text
freshflow_bao
freshflow_member_a
freshflow_member_b
```

Sau do moi nguoi doi `Database=...` trong connection string local.

