# ============================================================
# SG_SAUL_CATASTRO — Dockerfile para Backend API
# TODO: Sprint 1 — implementar build multi-stage de .NET 10
# ============================================================
#
# Estructura prevista:
#   Stage 1 (build):   mcr.microsoft.com/dotnet/sdk:10.0
#   Stage 2 (runtime): mcr.microsoft.com/dotnet/aspnet:10.0-alpine
#
# Publicar en: ghcr.io/sauronsaul/sg-catastro-api

FROM scratch
LABEL maintainer="Saul Gutierrez <sauronsaul.guti@gmail.com>"
LABEL description="SG_SAUL_CATASTRO API — placeholder Sprint 0"
