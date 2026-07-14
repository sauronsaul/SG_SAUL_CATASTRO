# ============================================================
# SG_SAUL_CATASTRO - Blazor WebAssembly publicado como estaticos
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/Directory.Build.props src/backend/
COPY src/backend/Directory.Packages.props src/backend/
COPY src/backend/SG.Contracts/SG.Contracts.csproj src/backend/SG.Contracts/
COPY src/frontend/SG.Web/SG.Web.csproj src/frontend/SG.Web/
RUN dotnet restore src/frontend/SG.Web/SG.Web.csproj

COPY src/backend/SG.Contracts/ src/backend/SG.Contracts/
COPY src/frontend/SG.Web/ src/frontend/SG.Web/
RUN dotnet publish src/frontend/SG.Web/SG.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM caddy:2-alpine AS runtime
COPY infra/docker/caddy/Caddyfile.web /etc/caddy/Caddyfile
COPY --from=build /app/publish/wwwroot /srv

EXPOSE 3000

LABEL maintainer="Saul Gutierrez <sauronsaul.guti@gmail.com>"
LABEL description="SG_SAUL_CATASTRO Web - Blazor WebAssembly y MapLibre"
