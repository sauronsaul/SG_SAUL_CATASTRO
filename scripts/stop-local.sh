#!/usr/bin/env bash
# ============================================================
# SG_SAUL_CATASTRO — Detención modo local
# Uso: bash scripts/stop-local.sh
# Los volúmenes NO se eliminan (los datos persisten).
# Para eliminar volúmenes usar: docker compose down -v
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_BASE="${PROJECT_ROOT}/infra/docker/docker-compose.yml"
COMPOSE_LOCAL="${PROJECT_ROOT}/infra/docker/docker-compose.local.yml"

echo ""
echo "Deteniendo servicios SG_CATASTRO (los datos persisten en volúmenes)..."
echo ""

docker compose \
  -f "${COMPOSE_BASE}" \
  -f "${COMPOSE_LOCAL}" \
  down

echo ""
echo "Servicios detenidos. Los volúmenes de datos se conservan."
echo ""
