#!/usr/bin/env bash
set -euo pipefail

# ── Validar argumento ──────────────────────────────────────────────────────────
if [[ $# -lt 1 ]]; then
    echo "ERROR: uso: scripts/commit.sh <archivo_mensaje>" >&2
    exit 1
fi

MSG_FILE="$1"

if [[ ! -f "$MSG_FILE" ]]; then
    echo "ERROR: archivo de mensaje no existe: $MSG_FILE" >&2
    exit 1
fi

# ── Verificar que hay cambios staged ──────────────────────────────────────────
if git diff --cached --quiet; then
    echo "ERROR: no hay cambios en el stage. Usá 'git add' antes de commitear." >&2
    exit 1
fi

# ── Escaneo de secretos sobre staged ─────────────────────────────────────────
LEAK_REPORT="/tmp/gitleaks_commit_$(date +%s).json"

echo "→ Escaneando cambios staged con gitleaks..."
if ! gitleaks git --staged --redact \
        --report-format json --report-path "$LEAK_REPORT" \
        --no-banner 2>&1; then
    echo "" >&2
    echo "╔══════════════════════════════════════════════════════════════╗" >&2
    echo "║  COMMIT BLOQUEADO — gitleaks detectó posibles secretos      ║" >&2
    echo "║  Revisá el reporte: $LEAK_REPORT" >&2
    echo "║  Usá --redact para no exponer el valor en logs.             ║" >&2
    echo "╚══════════════════════════════════════════════════════════════╝" >&2
    exit 1
fi

echo "→ Sin secretos detectados. Procediendo con el commit..."

# ── Commit ────────────────────────────────────────────────────────────────────
git -c commit.template= -c core.hooksPath=/dev/null commit --no-verify -F "$MSG_FILE"

# ── Verificar ausencia de trailer Co-Authored-By ─────────────────────────────
LAST_LINE=$(git log -1 --pretty=%B | tail -n 1)
if echo "$LAST_LINE" | grep -qi "Co-Authored-By"; then
    echo "" >&2
    printf '\033[0;31mWARNING: el mensaje de commit contiene "Co-Authored-By" en la última línea.\033[0m\n' >&2
    echo "Mensaje completo:" >&2
    git log -1 --pretty=%B >&2
    exit 1
fi

echo ""
echo "✓ Commit creado. Mensaje:"
git log -1 --pretty=%B
