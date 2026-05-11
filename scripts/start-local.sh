#!/usr/bin/env bash
# ============================================================
# SG_SAUL_CATASTRO — Inicio modo local
# Uso: bash scripts/start-local.sh
# ============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${PROJECT_ROOT}/.env"
ENV_EXAMPLE="${PROJECT_ROOT}/.env.example"
COMPOSE_BASE="${PROJECT_ROOT}/infra/docker/docker-compose.yml"
COMPOSE_LOCAL="${PROJECT_ROOT}/infra/docker/docker-compose.local.yml"

# --- Verificar que .env existe ---
if [ ! -f "${ENV_FILE}" ]; then
  echo ""
  echo "⚠  No se encontró .env en la raíz del proyecto."
  echo "   Copiando .env.example como .env..."
  cp "${ENV_EXAMPLE}" "${ENV_FILE}"
  echo ""
  echo "   IMPORTANTE: Edita el archivo .env y reemplaza los valores"
  echo "   marcados con <CAMBIAR> antes de continuar."
  echo ""
  read -r -p "   Presiona Enter cuando hayas editado .env, o Ctrl+C para cancelar..."
fi

echo ""
echo "Iniciando servicios SG_CATASTRO (modo local)..."
echo ""

docker compose \
  --env-file "${ENV_FILE}" \
  -f "${COMPOSE_BASE}" \
  -f "${COMPOSE_LOCAL}" \
  up -d --remove-orphans

echo ""
echo "Servicios levantados. URLs disponibles:"
echo ""

# Leer puertos del .env (con defaults)
POSTGRES_PORT=$(grep -E '^POSTGRES_PORT=' "${ENV_FILE}" | cut -d= -f2 | tr -d ' ' || echo "5432")
MINIO_API_PORT=$(grep -E '^MINIO_API_PORT=' "${ENV_FILE}" | cut -d= -f2 | tr -d ' ' || echo "9000")
MINIO_CONSOLE_PORT=$(grep -E '^MINIO_CONSOLE_PORT=' "${ENV_FILE}" | cut -d= -f2 | tr -d ' ' || echo "9001")
CADDY_HTTP_PORT=$(grep -E '^CADDY_HTTP_PORT=' "${ENV_FILE}" | cut -d= -f2 | tr -d ' ' || echo "80")

echo "  PostgreSQL   → localhost:${POSTGRES_PORT}"
echo "  MinIO API    → http://localhost:${MINIO_API_PORT}"
echo "  MinIO Consola→ http://localhost:${MINIO_CONSOLE_PORT}"
echo "  App (Caddy)  → http://localhost:${CADDY_HTTP_PORT}"
echo ""
echo "Para ver logs:  docker compose -f infra/docker/docker-compose.yml logs -f"
echo "Para detener:   bash scripts/stop-local.sh"
echo ""
