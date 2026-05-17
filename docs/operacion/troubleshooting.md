# Solución de problemas frecuentes

---

## La API no inicia — "JWT_SECRET no configurado"

**Síntoma**: El contenedor `sg_api` se reinicia en bucle. `docker logs sg_api` muestra:
```
System.InvalidOperationException: JWT_SECRET no configurado.
```

**Causa**: La variable `JWT_SECRET` no está definida en `.env` o el archivo `.env` no fue cargado.

**Solución**:
1. Verificar que `.env` existe en la raíz del proyecto.
2. Verificar que `JWT_SECRET` tiene un valor (no `<GENERAR_CON_...>`).
3. Generar un secret válido: `.\scripts\generate-jwt-secret.ps1`
4. Reiniciar: `bash scripts/start-local.sh`

---

## La API no inicia — "ADMIN_INITIAL_EMAIL / ADMIN_INITIAL_PASSWORD no configurado"

**Síntoma**: El contenedor `sg_api` falla con:
```
InvalidOperationException: ADMIN_INITIAL_EMAIL no configurado
```

**Causa**: Las credenciales del admin inicial no están en `.env`.

**Solución**:
1. Abrir `.env` y completar `ADMIN_INITIAL_EMAIL` y `ADMIN_INITIAL_PASSWORD`.
2. El password debe cumplir: mínimo 12 caracteres, 1 mayúscula, 1 minúscula, 1 dígito, 1 carácter especial (`!`, `@`, `#`, etc.).
3. Reiniciar el servicio: `docker compose -f infra/docker/docker-compose.yml restart api`

---

## Puerto 5434 en uso — PostgreSQL no inicia

**Síntoma**: `docker compose up` falla con:
```
Error response from daemon: Ports are not available: ... 5434
```

**Causa**: Otro proceso (instalación local de PostgreSQL, otro contenedor) ocupa el puerto 5434.

**Solución A**: Cambiar el puerto en `.env`:
```
POSTGRES_PORT=5435
```

**Solución B**: Identificar y detener el proceso que ocupa el puerto:
```powershell
# PowerShell
netstat -ano | findstr :5434
# Anotar el PID de la última columna
taskkill /PID <PID> /F
```

---

## Puerto 8080 en uso — API no inicia

**Síntoma**:
```
Error response from daemon: Ports are not available: ... 8080
```

**Solución**: Cambiar `BACKEND_PORT` en `.env` y reiniciar. Si usa IIS Express u otro proceso en 8080:
```powershell
netstat -ano | findstr :8080
```

---

## La API retorna 503 — "Unhealthy" en /health

**Síntoma**: `curl http://localhost:8080/health` retorna `{"status":"Unhealthy"}` o el contenedor se marca como unhealthy.

**Causa más probable**: La API no puede conectarse a PostgreSQL.

**Diagnóstico**:
```bash
# Ver logs de la API
docker logs sg_api --tail 50

# Verificar estado de postgres
docker inspect sg_postgres | grep -A5 Health
```

**Solución**:
1. Verificar que `sg_postgres` está corriendo: `docker compose ps`
2. Verificar que `ConnectionStrings__Default` en `.env` tiene `Password=` idéntico a `POSTGRES_PASSWORD`.
3. Reiniciar en orden: `docker compose -f infra/docker/docker-compose.yml restart postgres api`

---

## Login retorna 401 — credenciales correctas

**Síntoma**: POST `/api/auth/login` retorna 401 con credenciales que deberían funcionar.

**Causa A**: El seeder no creó el usuario admin porque `ADMIN_INITIAL_*` estaban mal configurados en el primer arranque.

**Diagnóstico**:
```bash
docker logs sg_api | grep -i "admin\|seed\|error"
```

**Solución**: Si el usuario no fue creado, conectarse a la base de datos y verificar:
```bash
docker exec -it sg_postgres psql -U sg_admin -d sg_catastro \
  -c "SELECT email FROM identidad.asp_net_users;"
```

Si no aparece el admin, vaciar la base de datos y reiniciar (atención: borra todos los datos):
```bash
docker volume rm sg_postgres_data
bash scripts/start-local.sh
```

**Causa B**: El usuario existe pero el password en `.env` fue cambiado después del primer arranque. El seeder omite usuarios existentes y no actualiza el password.

---

## Contenedor se reinicia en bucle

**Síntoma**: `docker compose ps` muestra `Restarting` para `sg_api`.

**Diagnóstico**:
```bash
docker logs sg_api --tail 100
```

Los mensajes de error más comunes:
- `JWT_SECRET no configurado` → ver sección JWT arriba.
- `Connection refused` → PostgreSQL no está healthy todavía, esperar 30s.
- `Migration failed` → ver sección Migraciones abajo.

---

## Error de migraciones al iniciar la API

**Síntoma**: Logs muestran error de EF Core durante el arranque:
```
Microsoft.EntityFrameworkCore.Database.Command: Failed executing DbCommand
```

**Causa más probable**: La extensión PostGIS no está instalada en la base de datos.

**Verificación**:
```bash
docker exec -it sg_postgres psql -U sg_admin -d sg_catastro \
  -c "SELECT extname FROM pg_extension WHERE extname = 'postgis';"
```

Si no aparece `postgis`, los scripts de inicialización en `database/init/` no se ejecutaron (solo corren en el primer arranque del contenedor).

**Solución**:
```bash
# Eliminar el volumen y reiniciar (borra todos los datos)
docker volume rm sg_postgres_data
bash scripts/start-local.sh
```

---

## Docker Desktop — WSL2 sin suficiente memoria

**Síntoma**: Contenedores muy lentos o errores de memoria en Docker Desktop en Windows.

**Solución**: Crear o editar `C:\Users\<usuario>\.wslconfig`:
```ini
[wsl2]
memory=4GB
processors=2
```

Reiniciar WSL2: `wsl --shutdown` en PowerShell, luego reiniciar Docker Desktop.

---

## Cómo ver logs en tiempo real

```bash
# Todos los servicios
docker compose -f infra/docker/docker-compose.yml logs -f

# Solo la API
docker compose -f infra/docker/docker-compose.yml logs -f api

# Solo PostgreSQL
docker compose -f infra/docker/docker-compose.yml logs -f postgres
```

Los logs de la API también se guardan en el volumen `sg_api_logs` y son accesibles via:
```bash
docker exec sg_api ls /app/logs/
docker exec sg_api cat /app/logs/sg-api-<fecha>.log
```
