#!/usr/bin/env bash
# Runs on the VPS from /opt/lifelink/deploy/vps. Idempotent: secrets are generated only on the first run.
set -euo pipefail
cd "$(dirname "$0")"

if [[ ! -f .env ]]; then
  umask 077
  rand() { openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c "$1"; }
  cat > .env <<EOF
POSTGRES_PASSWORD=$(rand 32)
JWT_SIGNING_KEY=$(rand 64)
AGENT_INTERNAL_API_KEY=$(rand 40)
BOOTSTRAP_ADMIN_EMAIL=admin@lifelink.local
BOOTSTRAP_ADMIN_PASSWORD=Ad1!$(rand 16)
DEMO_DATA_ENABLED=true
DEMO_DATA_PASSWORD=Dm1!$(rand 14)
EOF
  echo "Generated new secrets in $(pwd)/.env"
fi

docker compose up -d --build
for i in $(seq 1 60); do
  if curl -fsS http://127.0.0.1:5090/health >/dev/null 2>&1 && curl -fsS http://127.0.0.1:5091/health/api >/dev/null 2>&1; then echo "API and web healthy"; exit 0; fi
  sleep 3
done
echo "API did not become healthy" >&2
docker compose logs --tail 80 api >&2
exit 1
