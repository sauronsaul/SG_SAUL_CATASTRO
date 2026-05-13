# ADR 0015 — pg_hba.conf: scram-sha-256 para todas las conexiones

**Fecha**: 2026-05-12
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

La imagen `postgis/postgis:16-3.4-alpine` (basada en `postgres:16-alpine`)
genera por defecto un `pg_hba.conf` con autenticación `trust` para conexiones
por socket Unix (tipo `local`):

```
local   all   all   trust
host    all   all   127.0.0.1/32   trust
```

Esto significa que cualquier proceso dentro del contenedor puede conectarse a
PostgreSQL sin password:

```bash
docker exec sg_postgres psql -U sg_admin -d sg_catastro -c "SELECT 1;"
# Funciona sin password — agujero de seguridad
```

Durante el Checkpoint 1.2 esto provocó un **falso positivo en la validación**:
la migración M001 se aplicó vía `psql` dentro del contenedor (sin password),
dando por válido un flujo que nunca fue probado realmente. El flujo real
(.NET → TCP → password) falló por un problema distinto en la connection string.

## Decisión

Se reemplaza el `pg_hba.conf` generado por `initdb` con uno explícito que
requiere `scram-sha-256` para **todas** las conexiones, incluyendo las locales
por socket Unix.

```
# infra/docker/postgres/pg_hba.conf
local   all   all                   scram-sha-256
host    all   all   127.0.0.1/32    scram-sha-256
host    all   all   ::1/128         scram-sha-256
host    all   all   0.0.0.0/0       scram-sha-256
```

Se monta como bind mount y se activa con:
```yaml
command: postgres -c hba_file=/etc/postgresql/pg_hba.conf
environment:
  POSTGRES_INITDB_ARGS: "--auth-host=scram-sha-256 --auth-local=scram-sha-256"
  POSTGRES_HOST_AUTH_METHOD: scram-sha-256
```

## Justificación

**Consistencia local/servidor.**

El entorno de desarrollo se comporta igual que producción. Un sistema que solo
funciona porque el auth está desactivado en dev es un sistema que no ha sido
probado realmente.

**Cierre del falso positivo.**

Si `docker exec sg_postgres psql -U sg_admin -d sg_catastro` falla con error de
autenticación, confirma que el endurecimiento está activo. Si funciona sin
password, el `pg_hba.conf` no se está aplicando.

**Escalación trivial.**

Con `trust` para conexiones locales, cualquier proceso comprometido dentro del
contenedor tiene acceso completo a PostgreSQL sin conocer el password. Con
`scram-sha-256`, necesita el password incluso desde dentro.

**El healthcheck sigue funcionando.**

`pg_isready` realiza solo un handshake TCP — no completa autenticación. Reporta
"accepting connections" independientemente del método de auth configurado.

## Cómo verificar que el endurecimiento está activo

```bash
# Debe FALLAR (no password):
docker exec sg_postgres psql -U sg_admin -d sg_catastro -c "\dn"

# Debe FUNCIONAR (con password vía PGPASSWORD):
docker exec -e "PGPASSWORD=<password>" sg_postgres psql -U sg_admin -d sg_catastro -c "\dn"
```

## Consecuencias

**Positivas**:
- El flujo de autenticación real (.NET → TCP → scram-sha-256) es el único camino.
- No es posible aplicar migraciones "de contrabando" via socket sin validar auth.
- El sistema se comporta igual en local y servidor.

**Negativas / compromisos**:
- Los scripts de diagnóstico que usen `docker exec psql` deben incluir
  `-e "PGPASSWORD=..."` o usar un archivo `.pgpass`.
- Si se olvida el password, no hay fallback — es necesario recrear el volumen.
  Documentar el password en un gestor de contraseñas seguro.
