#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# RAGGit workstation bootstrap - PRIVATE CLOUD mode (feature 031).
#
# Brings a fresh cloud machine to a serving workstation: Docker + native Ollama,
# the data volume mounted, TLS certificate, the production stack up, models
# pulled, and the first admin account created. Safe to re-run (idempotent).
#
# Local mode does NOT use this script - run the root docker-compose.yml instead
# (see docs/runbooks/workstation-deployment.md).
#
# Preconditions:
#   * Ubuntu/Debian host with sudo, reached over SSH by the operator
#   * the repository checked out on the host (this script lives in <repo>/deploy/)
#   * the block-storage volume attached and mounted at RAGGIT_DATA_DIR
#   * deploy/.env filled in from deploy/.env.example
#
# Usage:  sudo ./deploy/bootstrap.sh
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${SCRIPT_DIR}/.env"
COMPOSE_FILE="${SCRIPT_DIR}/docker-compose.prod.yml"

log()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m[warn] %s\033[0m\n' "$*" >&2; }
die()  { printf '\033[1;31m[error] %s\033[0m\n' "$*" >&2; exit 1; }

[[ "${EUID}" -eq 0 ]] || die "run with sudo (installs packages and systemd units)"
[[ -f "${ENV_FILE}" ]] || die "missing ${ENV_FILE} - copy deploy/.env.example to deploy/.env and fill it in"
[[ -f "${COMPOSE_FILE}" ]] || die "missing ${COMPOSE_FILE}"

# shellcheck disable=SC1090
set -a; . "${ENV_FILE}"; set +a

for var in WORKSTATION_HOST BIND_ADDRESS RAGGIT_DATA_DIR ADMIN_API_KEY EMPLOYEE_API_KEY \
           CERT_PFX_PATH CERT_PFX_PASSWORD ADMIN_USERNAME ADMIN_DISPLAY_NAME ADMIN_PASSWORD; do
    [[ -n "${!var:-}" ]] || die "${var} is not set in ${ENV_FILE}"
done

EMBED_MODEL="${EMBED_MODEL:-snowflake-arctic-embed2}"
CHAT_MODEL="${CHAT_MODEL:-qwen2.5:3b}"
OLLAMA_URL="${OLLAMA_URL:-http://host.docker.internal:11434}"

# The certificate path in .env is a CONTAINER path under the mounted volume.
HOST_DATA_DIR="${RAGGIT_DATA_DIR%/}"
HOST_CERT="${HOST_DATA_DIR}${CERT_PFX_PATH#/app/data}"

# ---------------------------------------------------------------------------
log "Checking the data volume is mounted at ${HOST_DATA_DIR}"
mountpoint -q "${HOST_DATA_DIR}" || warn "${HOST_DATA_DIR} is not a mountpoint - is the volume attached and mounted?"
mkdir -p "${HOST_DATA_DIR}/certs" "${HOST_DATA_DIR}/documents"

# ---------------------------------------------------------------------------
log "Installing Docker (skipped if present)"
if ! command -v docker >/dev/null 2>&1; then
    apt-get update
    apt-get install -y ca-certificates curl gnupg openssl
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    . /etc/os-release
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" > /etc/apt/sources.list.d/docker.list
    apt-get update
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
docker compose version >/dev/null 2>&1 || die "docker compose v2 plugin is required"

# ---------------------------------------------------------------------------
log "Installing Ollama (skipped if present)"
if ! command -v ollama >/dev/null 2>&1; then
    curl -fsSL https://ollama.com/install.sh | sh
fi

# The container reaches Ollama through host.docker.internal, so Ollama must listen
# on the host's interfaces. WARNING: the cloud firewall MUST deny 11434 from
# everywhere - this port is never public. See R-07 in the runbook.
log "Ensuring Ollama listens for the container"
mkdir -p /etc/systemd/system/ollama.service.d
cat > /etc/systemd/system/ollama.service.d/raggit.conf <<'EOF'
[Service]
Environment="OLLAMA_HOST=0.0.0.0"
EOF
systemctl daemon-reload
systemctl enable --now ollama
systemctl restart ollama

# `systemctl restart` returns before the server is accepting connections, so
# pulling immediately races the restart and fails. Wait for the API instead.
log "Waiting for the Ollama API to become ready"
ollama_ready=""
for attempt in $(seq 1 30); do
    if ollama list >/dev/null 2>&1; then
        ollama_ready="yes"
        break
    fi
    sleep 2
done
[[ -n "${ollama_ready}" ]] || die "Ollama did not become ready after 60s; check 'systemctl status ollama'"
log "Ollama is ready ($(ollama --version 2>/dev/null | head -1))"

log "Pulling models: ${EMBED_MODEL} and ${CHAT_MODEL}"
ollama pull "${EMBED_MODEL}"
ollama pull "${CHAT_MODEL}"

# ---------------------------------------------------------------------------
if [[ -f "${HOST_CERT}" ]]; then
    log "TLS certificate already present at ${HOST_CERT}"
else
    log "Generating a self-signed certificate for ${WORKSTATION_HOST}"
    command -v openssl >/dev/null 2>&1 || apt-get install -y openssl
    tmp="$(mktemp -d)"
    trap 'rm -rf "${tmp}"' EXIT
    # An IP literal needs an IP SAN; a host name needs a DNS SAN.
    if [[ "${WORKSTATION_HOST}" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]]; then
        san="IP:${WORKSTATION_HOST}"
    else
        san="DNS:${WORKSTATION_HOST}"
    fi
    openssl req -x509 -newkey rsa:2048 -sha256 -days 825 -nodes \
        -keyout "${tmp}/key.pem" -out "${tmp}/cert.pem" \
        -subj "/CN=${WORKSTATION_HOST}" -addext "subjectAltName=${san}" >/dev/null 2>&1
    openssl pkcs12 -export -out "${HOST_CERT}" \
        -inkey "${tmp}/key.pem" -in "${tmp}/cert.pem" \
        -passout "pass:${CERT_PFX_PASSWORD}" >/dev/null 2>&1
    # The container runs non-root and must be able to read the PFX. It stays
    # password-protected, and the host is reachable only from the company network.
    chmod 644 "${HOST_CERT}"
    cp "${tmp}/cert.pem" "${HOST_DATA_DIR}/certs/raggit.crt"
    log "Wrote ${HOST_CERT} (and ${HOST_DATA_DIR}/certs/raggit.crt for client trust)"
fi

# ---------------------------------------------------------------------------
log "Bringing the stack up"
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d --build

log "Waiting for https://${WORKSTATION_HOST}/health"
for attempt in $(seq 1 30); do
    if curl -sk --max-time 5 "https://${WORKSTATION_HOST}/health" | grep -q '"vectorDb"'; then
        curl -sk "https://${WORKSTATION_HOST}/health"; echo
        break
    fi
    [[ "${attempt}" -eq 30 ]] && die "workstation did not become healthy after 30 attempts; check 'docker compose logs'"
    sleep 5
done

# ---------------------------------------------------------------------------
log "Creating the first admin account (idempotent)"
# The CLI exits 2 when the username already exists, which is fine on a re-run.
set +e
printf '%s\n' "${ADMIN_PASSWORD}" | docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" \
    exec -T raggit-workstation \
    dotnet RAGGit.Workstation.Api.dll user add \
    --username "${ADMIN_USERNAME}" \
    --display-name "${ADMIN_DISPLAY_NAME}" \
    --role Admin \
    --password-stdin
rc=$?
set -e
case "${rc}" in
    0) log "Admin '${ADMIN_USERNAME}' created" ;;
    2) log "Admin '${ADMIN_USERNAME}' already exists - nothing to do" ;;
    *) die "operator CLI failed with exit code ${rc}" ;;
esac

log "Bootstrap complete"
cat <<EOF

Workstation:  https://${WORKSTATION_HOST}
Health:       https://${WORKSTATION_HOST}/health
Data volume:  ${HOST_DATA_DIR}

Next:
  1. Confirm the cloud firewall allows 443 only from the company's networks and
     denies 11434 (and everything else) from outside - see the runbook's boundary check.
  2. Copy deploy/appsettings.client.template.json to each PC as appsettings.json,
     set the URL and employee key, and trust ${HOST_DATA_DIR}/certs/raggit.crt.
EOF
