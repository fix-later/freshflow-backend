# Prompt: FreshFlow Firewall IP Whitelist Setup

Copy toàn bộ nội dung phần **"## Prompt"** bên dưới và paste vào Claude trên VPS.

---

## Prompt

You are configuring the firewall on this VPS to restrict access to the FreshFlow database ports (PostgreSQL 5433, Redis 6379). Only whitelisted IP addresses should be able to connect.

**Goal:** Use `ufw` to allow specific IPs and block everyone else on ports 5433 and 6379. SSH (port 22) must remain open. Do not touch any other ports or existing rules.

Work through each step below. After every command, show the output before continuing.

---

### Step 1 — Check current firewall status

```bash
ufw status verbose
```

Note the existing rules so nothing gets accidentally removed.

---

### Step 2 — Ensure SSH is allowed (do this BEFORE enabling ufw)

```bash
ufw allow 22/tcp comment 'SSH'
```

> Critical: if ufw is not yet enabled, this prevents lockout when we enable it.

---

### Step 3 — Add whitelisted IPs for PostgreSQL (port 5433)

```bash
# --- Team member IPs ---
ufw allow from 171.236.48.103 to any port 5433 proto tcp comment 'DB - Bao (lead)'

# Add more team members below when needed:
# ufw allow from <MEMBER_IP> to any port 5433 proto tcp comment 'DB - <Name>'
```

---

### Step 4 — Add whitelisted IPs for Redis (port 6379)

```bash
# --- Team member IPs ---
ufw allow from 171.236.48.103 to any port 6379 proto tcp comment 'Redis - Bao (lead)'

# Add more team members below when needed:
# ufw allow from <MEMBER_IP> to any port 6379 proto tcp comment 'Redis - <Name>'
```

---

### Step 5 — Block all other traffic on ports 5433 and 6379

```bash
ufw deny 5433/tcp comment 'Block public PostgreSQL'
ufw deny 6379/tcp comment 'Block public Redis'
```

> Order matters in ufw: the ALLOW rules added in steps 3–4 come before these DENY rules, so whitelisted IPs are permitted first.

---

### Step 6 — Enable ufw (if not already enabled)

```bash
ufw --force enable
```

---

### Step 7 — Verify the final ruleset

```bash
ufw status numbered
```

Expected output should show:
- Port 22 ALLOW from Anywhere
- Port 5433 ALLOW from 171.236.48.103
- Port 6379 ALLOW from 171.236.48.103
- Port 5433 DENY from Anywhere
- Port 6379 DENY from Anywhere

---

### Step 8 — Smoke test connectivity

Run these checks from the VPS itself (loopback still works for the app):

```bash
# PostgreSQL local access must still work
docker exec freshflow-postgres pg_isready -U freshflow_app -d freshflow
echo "PostgreSQL local: $?"

# Redis local access must still work
docker exec freshflow-redis redis-cli -a "$(grep REDIS_PASSWORD /opt/freshflow/infra/.env | cut -d= -f2)" PING
echo "Redis local: $?"
```

---

### Step 9 — Report results

Output a summary containing:
1. Full output of `ufw status numbered`
2. Whether PostgreSQL and Redis local smoke tests passed
3. Instructions for adding a new team member IP in the future (show the exact commands with placeholder `<NEW_IP>` and `<Name>`)

---

**Rules for Claude on VPS:**
- Do NOT disable or remove existing ufw rules for other ports
- Do NOT remove the SSH allow rule
- If ufw is not installed, install it first: `apt-get install -y ufw`
- If any step fails, stop and report the full error before continuing
- ufw rule order matters — ALLOW rules for specific IPs must be added BEFORE the DENY catch-all rules
