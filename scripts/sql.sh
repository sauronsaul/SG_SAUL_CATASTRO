#!/usr/bin/env bash
# Acceso SQL autorizado en desarrollo (ver AGENTS.md).
# El ejecutor (Codex) usa este script y NUNCA referencia la variable
# de la contraseña directamente. El valor jamás se imprime.
set -euo pipefail
if [ $# -lt 1 ]; then echo "uso: scripts/sql.sh \"<SQL>\"" >&2; exit 1; fi
docker exec sg_postgres bash -c \
  'PGPASSWORD="$POSTGRES_PASSWORD" psql -U sg_admin -d sg_catastro -P pager=off -v ON_ERROR_STOP=1 -c "$1"' \
  sql "$1"
