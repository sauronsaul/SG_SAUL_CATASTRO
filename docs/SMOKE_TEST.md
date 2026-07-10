# Smoke test de paridad containerizada

La paridad se valida con la misma composición local que usa el proyecto. El
contenedor API declara `SG_APPLY_MIGRATIONS=true`; la ejecución nativa no lo
declara por defecto.

Desde la raíz del repositorio:

```powershell
docker compose --env-file .env -f infra/docker/docker-compose.yml -f infra/docker/docker-compose.local.yml up -d --build
docker compose --env-file .env -f infra/docker/docker-compose.yml -f infra/docker/docker-compose.local.yml ps
curl.exe -fsS http://localhost/health
```

Resultado esperado:

- `sg_api` aparece como `running` o `healthy` después de completar su
  healthcheck.
- `curl.exe` responde `Healthy` a través de Caddy (`http://localhost/health`).
- La ruta `/health` se reenvía a `api:8080`; no depende del frontend.

Para investigar una falla sin exponer secretos:

```powershell
docker compose --env-file .env -f infra/docker/docker-compose.yml -f infra/docker/docker-compose.local.yml logs --tail=100 api caddy
```
