# ADR 0017 — Puerto 5434 para el contenedor PostgreSQL en desarrollo local

**Fecha**: 2026-05-12
**Estado**: Aceptado (actualizado — ver historial)
**Autor**: Saul Gutierrez

---

## Historial de revisiones

| Versión | Puerto elegido | Motivo del cambio |
|---|---|---|
| v1 (inicial) | 5433 | PG16 ocupa 5432 |
| v2 (actual) | 5434 | PG16 ocupa 5432 Y PG17 ocupa 5433 |

---

## Contexto

La máquina de desarrollo tiene **dos instalaciones nativas de PostgreSQL en Windows**:

| Servicio | Puerto | PID típico |
|---|---|---|
| `postgresql-x64-16` | 5432 | escucha en `0.0.0.0:5432` |
| `postgresql-x64-17` | 5433 | escucha en `0.0.0.0:5433` |

Docker Desktop expone contenedores en `:::PPPP` (IPv6) Y el proceso nativo
ocupa `0.0.0.0:PPPP` (IPv4). Cuando Npgsql conecta a `localhost:5433`:

- En Windows, `localhost` puede resolverse a `127.0.0.1` (IPv4).
- `127.0.0.1:5433` llega al PostgreSQL 17 nativo (`0.0.0.0:5433`).
- El contenedor Docker **nunca recibe la conexión**.
- PostgreSQL 17 rechaza con `28P01` porque no tiene el usuario `sg_admin`.

El mismo problema ocurrió primero con el puerto 5432 (PG16) y luego con
el 5433 (PG17). El diagnóstico definitivo fue:

```
Get-NetTCPConnection -LocalPort 5433:
  0.0.0.0:5433  → postgres PID 6624 (postgresql-x64-17) ← intercepta IPv4
  :::5433       → com.docker.backend                    ← solo IPv6
```

Y verificado en `C:\Program Files\PostgreSQL\17\data\postgresql.conf`:
```
port = 5433
```

## Decisión

El contenedor PostgreSQL del proyecto usa el **puerto 5434** en el host.

```yaml
# docker-compose.yml
ports:
  - "${POSTGRES_PORT:-5434}:5432"
```

```
# .env / .env.example
POSTGRES_PORT=5434
ConnectionStrings__Default=Host=localhost;Port=5434;...
```

## Regla operativa

En cualquier máquina con instalaciones locales de PostgreSQL en Windows:
verificar qué puertos están en uso antes de elegir el puerto del contenedor.

```powershell
# Listar puertos PostgreSQL ocupados en Windows:
Get-NetTCPConnection -State Listen | Where-Object {
    (Get-Process -Id $_.OwningProcess -EA SilentlyContinue).ProcessName -eq 'postgres'
} | Select-Object LocalPort, OwningProcess

# Verificar puerto candidato libre:
Get-NetTCPConnection -LocalPort 5434 -EA SilentlyContinue
# Sin output = libre.
```

## Consecuencias

**Positivas**:
- `dotnet ef database update` funciona sin conflicto de puertos.
- Las instalaciones locales de PostgreSQL 16 y 17 no se ven afectadas.

**Negativas / compromisos**:
- En esta máquina, el puerto efectivo es 5434. Documentar para conexiones
  externas (DBeaver, pgAdmin, etc.). Usar siempre `.env` como fuente de
  verdad del puerto.
- Si en el futuro se instala PG18 con otro puerto conflictivo, repetir
  el diagnóstico.
