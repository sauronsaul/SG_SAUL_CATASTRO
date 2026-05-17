# Instalación — Modo Local

Guía completa para instalar SG_SAUL_CATASTRO en una sola PC con Docker Desktop (municipio pequeño sin servidor).

---

## Prerrequisitos

| Componente | Versión mínima | Verificación |
|---|---|---|
| Windows 10/11 | Build 19041+ | `winver` |
| Docker Desktop | 29.4+ | `docker --version` |
| Git | 2.54+ | `git --version` |

Docker Desktop debe estar corriendo antes de ejecutar cualquier comando. No se requiere .NET SDK ni Node.js para el modo operativo (solo para desarrollo).

---

## Pasos de instalación

### 1. Clonar el repositorio

```bash
git clone git@github.com:sauronsaul/SG_SAUL_CATASTRO.git
cd SG_SAUL_CATASTRO
```

### 2. Crear el archivo `.env`

```bash
cp .env.example .env
```

Abrir `.env` con un editor de texto y reemplazar **todos** los valores marcados con `<CAMBIAR>`:

| Variable | Descripción | Ejemplo seguro |
|---|---|---|
| `POSTGRES_PASSWORD` | Contraseña de PostgreSQL | Solo alfanumérico + `-_.` |
| `MINIO_ROOT_PASSWORD` | Contraseña de MinIO | Mínimo 8 caracteres |
| `JWT_SECRET` | Clave de firma JWT | Generar con el script abajo |
| `ADMIN_INITIAL_EMAIL` | Email del primer admin | `admin@caranavi.gob.bo` |
| `ADMIN_INITIAL_PASSWORD` | Password del primer admin | Mínimo 12 chars, 1 mayúscula, 1 dígito, 1 especial |

**Generar JWT_SECRET (PowerShell):**

```powershell
.\scripts\generate-jwt-secret.ps1
```

Copiar la salida del script y pegarla como valor de `JWT_SECRET` en `.env`.

> **IMPORTANTE**: Nunca versionar el archivo `.env`. Está en `.gitignore` por diseño.

### 3. Primer arranque

```bash
bash scripts/start-local.sh
```

El script detecta automáticamente si `.env` existe y, si no, lo copia desde `.env.example` antes de continuar.

**En el primer arranque**, Docker Desktop descarga las imágenes base (~600 MB). Esto puede tardar varios minutos según la velocidad de conexión. Las siguientes ejecuciones son instantáneas porque Docker usa el caché local.

El proceso completo de primer arranque:

1. Docker descarga imágenes (si no están en caché).
2. PostgreSQL inicia y aplica los scripts de inicialización en `database/init/`.
3. La API espera a que PostgreSQL esté healthy (~30s).
4. La API aplica migraciones EF Core automáticamente.
5. La API ejecuta el seeder: crea roles (`Admin`, `Tecnico`, `Operador`, `Consulta`) y el usuario administrador con las credenciales del `.env`.
6. Caddy inicia y comienza a rutear tráfico.

### 4. Verificar que todo funciona

```bash
# Estado de los contenedores
docker compose -f infra/docker/docker-compose.yml ps

# Health de la API
curl http://localhost:8080/health
# Esperado: {"status":"Healthy"}

# Login de prueba (reemplazar con tus credenciales del .env)
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@caranavi.gob.bo","password":"TuPassword123!"}'
# Esperado: {"accessToken":"eyJ...","refreshToken":"...","usuario":{...}}
```

El script `start-local.sh` muestra las URLs al terminar:

```
  PostgreSQL   → localhost:5434
  MinIO API    → http://localhost:9000
  MinIO Consola→ http://localhost:9001
  API Backend  → http://localhost:8080
  API Health   → http://localhost:8080/health
  API Swagger  → http://localhost:8080/openapi/v1.json
  App (Caddy)  → http://localhost:80
  API via Caddy→ http://localhost:80/api/auth/login
```

---

## Detener el sistema

```bash
bash scripts/stop-local.sh
```

Los datos persisten en volúmenes Docker (`sg_postgres_data`, `sg_minio_data`, `sg_api_logs`). Al volver a iniciar con `start-local.sh` los datos estarán intactos.

---

## Actualización

Para actualizar a una versión nueva del sistema:

```bash
# 1. Obtener los cambios
git pull

# 2. Reconstruir la imagen de la API (--build fuerza rebuild)
docker compose \
  --env-file .env \
  -f infra/docker/docker-compose.yml \
  -f infra/docker/docker-compose.local.yml \
  up -d --build api

# 3. Verificar health
curl http://localhost:8080/health
```

Las migraciones EF Core se aplican automáticamente al arrancar la nueva imagen. No se pierden datos.

---

## Credenciales iniciales

| Sistema | Usuario | Contraseña |
|---|---|---|
| API / Aplicación | Valor de `ADMIN_INITIAL_EMAIL` en `.env` | Valor de `ADMIN_INITIAL_PASSWORD` en `.env` |
| MinIO Consola | Valor de `MINIO_ROOT_USER` en `.env` | Valor de `MINIO_ROOT_PASSWORD` en `.env` |
| PostgreSQL | Valor de `POSTGRES_USER` en `.env` | Valor de `POSTGRES_PASSWORD` en `.env` |

**Cambiar el password del admin**: actualmente vía la API (endpoint pendiente en Sprint 3+). Por ahora, modificar `ADMIN_INITIAL_PASSWORD` en `.env` y recrear el contenedor de la API (`docker compose up -d --force-recreate api`) solo funciona si el usuario admin aún no existe en la base de datos. Si ya existe, el seeder lo omite.

---

## Acceso desde otra PC en la misma red (opcional)

En modo local el sistema está diseñado para una sola PC. Para acceso temporal desde otra PC de la misma red:

1. Obtener la IP local de la PC con Docker: `ipconfig` → buscar "IPv4".
2. Acceder via `http://<IP-local>:<BACKEND_PORT>/api/...` directamente o via `http://<IP-local>:<CADDY_HTTP_PORT>/api/...` si Caddy está corriendo.

Para acceso multiusuario estable, usar el modo servidor (Sprint 7+).
