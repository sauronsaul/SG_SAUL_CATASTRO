# ============================================================
# SG_SAUL_CATASTRO — Dockerfile para Frontend Web
# TODO: Sprint 4 — implementar build multi-stage de Vite + React
# ============================================================
#
# Estructura prevista:
#   Stage 1 (build):   node:22-alpine — npm run build
#   Stage 2 (runtime): caddy:2-alpine — servir dist/ estático
#
# Publicar en: ghcr.io/sauronsaul/sg-catastro-web

FROM scratch
LABEL maintainer="Saul Gutierrez <sauronsaul.guti@gmail.com>"
LABEL description="SG_SAUL_CATASTRO Web — placeholder Sprint 0"
