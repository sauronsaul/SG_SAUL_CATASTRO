# ============================================================
# SG_SAUL_CATASTRO — Backend API — Build multi-stage .NET 10
# Build context: raíz del repositorio (context: ../..)
# ============================================================

# Stage 1: compilación y publicación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar manifiestos de paquetes primero para aprovechar la caché de capas.
# Si solo cambia código (no dependencias), Docker reutiliza la capa de restore.
# Contexto es la raíz del repositorio; src/backend/ contiene los proyectos.
COPY src/backend/Directory.Build.props src/backend/Directory.Packages.props ./
COPY src/backend/SG.Api/SG.Api.csproj             SG.Api/
COPY src/backend/SG.Application/SG.Application.csproj SG.Application/
COPY src/backend/SG.Domain/SG.Domain.csproj       SG.Domain/
COPY src/backend/SG.Infrastructure/SG.Infrastructure.csproj SG.Infrastructure/
COPY src/backend/SG.Contracts/SG.Contracts.csproj SG.Contracts/

RUN dotnet restore SG.Api/SG.Api.csproj

# Copiar el resto del código y publicar en Release
COPY src/backend/SG.Api/           SG.Api/
COPY src/backend/SG.Application/   SG.Application/
COPY src/backend/SG.Domain/        SG.Domain/
COPY src/backend/SG.Infrastructure/ SG.Infrastructure/
COPY src/backend/SG.Contracts/     SG.Contracts/

RUN dotnet publish SG.Api/SG.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Stage 2: runtime Alpine mínimo
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# ICU: requerido por CultureInfo("es-BO") y formato de fechas/números bolivianos.
# tzdata: zona horaria America/La_Paz.
RUN apk add --no-cache icu-libs tzdata

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV TZ=America/La_Paz
ENV ASPNETCORE_URLS=http://+:8080

# Usuario no-root para reducir superficie de ataque
RUN addgroup -S sgapp && adduser -S sgapp -G sgapp

# Directorio de logs con permisos correctos antes de cambiar usuario
RUN mkdir -p /app/logs && chown sgapp:sgapp /app/logs

COPY --from=build /app/publish .
RUN chown -R sgapp:sgapp /app

USER sgapp

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SG.Api.dll"]
