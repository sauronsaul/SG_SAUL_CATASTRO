# AGENTS.md — Contexto Maestro de SG_SAUL_CATASTRO

> **Archivo crítico**: Codex DEBE leer este archivo al inicio de CADA sesión, antes de cualquier otra acción. Este archivo es la fuente de verdad del proyecto.

---

## 1. Identidad del proyecto

**Nombre**: SG_SAUL_CATASTRO
**Tipo**: Sistema institucional de gestión catastral municipal
**Municipio piloto**: Caranavi, La Paz, Bolivia
**Mantenedor único**: Saul Gutierrez
**Repositorio**: https://github.com/sauronsaul/SG_SAUL_CATASTRO.git

### Lo que SÍ es

Un sistema integral que administra de forma coherente: predios, propietarios, titularidad, geometrías, cartografía, zonificación, valuación, autoavalúo, mutaciones, trámites, certificados, documentos adjuntos, historial, auditoría, usuarios, roles, permisos, reportes, consultas y trazabilidad institucional.

### Lo que NO es

- No es un visor GIS aislado.
- No es una ficha predial simple.
- No es un generador de certificados independiente.
- No es un ERP genérico.
- No es una aplicación de escritorio improvisada.
- No es un conjunto de formularios sin dominio definido.

---

## 2. Modelo de despliegue dual

El sistema soporta **dos modos de despliegue** con el **mismo código fuente**:

| Modo | Audiencia | Topología |
|---|---|---|
| **Local** (prioridad MVP) | Municipios pequeños sin servidor | Docker Desktop en una sola PC, operador único, `localhost` |
| **Servidor** (fase posterior) | Municipios con infraestructura | Docker Compose en servidor, múltiples usuarios, acceso web |

Mismo `docker-compose.yml` base. Diferencias en `docker-compose.local.yml` y `docker-compose.prod.yml`.

**Implicación**: Codex construye UN solo sistema. Las diferencias entre modos son archivos de configuración, no ramas de código distintas.

---

## 3. Stack tecnológico OFICIAL — NO MODIFICAR sin autorización

### Backend
- **.NET 10 LTS** (soporte hasta noviembre 2028)
- **C# 13**
- **ASP.NET Core 10**
- **Entity Framework Core 10**
- **NetTopologySuite** (geometría)
- **MediatR** (CQRS)
- **FluentValidation**
- **Mapster** (mapeo)
- **QuestPDF** (PDFs)
- **QRCoder** (QR)
- **ASP.NET Core Identity + JWT Bearer + BCrypt.Net-Next**
- **Serilog** (logs estructurados)
- **Swashbuckle** (Swagger UI)
- **xUnit + FluentAssertions + Testcontainers** (testing)

### Frontend
- **Vite 5+** (build tool)
- **React 18+ + TypeScript 5+**
- **Ant Design 5** (`antd`) — librería de UI principal
- **Ant Design Pro Components** (ProTable, ProForm)
- **TanStack Query** (estado servidor)
- **Zustand** (estado cliente)
- **React Router 6+**
- **Axios** (HTTP)
- **dayjs** con locale `es` y timezone `America/La_Paz`
- **MapLibre GL JS + react-map-gl** (mapas)
- **Vitest + React Testing Library** (testing)

### Datos e infraestructura
- **PostgreSQL 16** + **PostGIS 3.4**
- **MinIO** (almacenamiento S3-compatible para adjuntos)
- **pg_tileserv** (vector tiles desde PostGIS)
- **tileserver-gl** (tiles base offline)
- **Caddy** (reverse proxy / TLS automático)
- **Docker + Docker Compose**

### Calidad y operación
- **GitHub Actions** (CI/CD)
- **GitHub Container Registry (ghcr.io)** (imágenes Docker)
- **Serilog → Console + File rolling** (logs)
- **pg_dump + scripts bash** (backups)

### SRID base
- **EPSG:32719** (UTM WGS84 zona 19 Sur — Bolivia occidental)

### Localización (Bolivia)
- Backend: `CultureInfo("es-BO")` global.
- Frontend: Ant Design `ConfigProvider` con `locale={esES}` + `dayjs.locale('es')` + timezone `America/La_Paz`.
- Formato fecha: `dd/MM/yyyy` y `dd/MM/yyyy HH:mm`.
- Formato número: `1.234,56`.
- Moneda: `Bs. 1.234,56`.

---

## 4. Arquitectura — Clean Architecture

```
Presentación (React, QGIS, QField)
       ↓ HTTP REST/JSON
SG.Api (Controllers, Middleware)
       ↓
SG.Application (MediatR handlers, DTOs, Validators)
       ↓
SG.Domain (Entidades, Value Objects, Reglas) ← núcleo, sin dependencias
       ↑
SG.Infrastructure (EF Core, MinIO, Auth, Logs)
       ↓
PostgreSQL 16 + PostGIS 3.4
```

### Reglas estrictas de capas

1. **El dominio NO depende de nada externo** (ni de EF Core, ni de ASP.NET, ni de paquetes de infraestructura).
2. **La interfaz NO contiene reglas catastrales, NO ejecuta SQL directo, NO calcula valuaciones, NO modifica lógica de negocio.**
3. **La infraestructura conoce al dominio**, no al revés.
4. **La API conoce a la aplicación**, no al revés.
5. **El frontend conoce solo a la API vía contratos (DTOs)**.

---

## 5. Estructura del repositorio

```
SG_SAUL_CATASTRO/
├── AGENTS.md                    ← este archivo
├── README.md
├── .gitignore  .editorconfig  .env.example
│
├── src/
│   ├── backend/
│   │   ├── SG.Api/              ← entry point ASP.NET Core
│   │   ├── SG.Application/      ← casos de uso, DTOs
│   │   ├── SG.Domain/           ← entidades, VOs, reglas
│   │   ├── SG.Infrastructure/   ← EF Core, MinIO, Auth
│   │   ├── SG.Contracts/        ← DTOs públicos compartidos
│   │   ├── SG.slnx
│   │   └── tests/
│   │       ├── SG.Domain.Tests/
│   │       ├── SG.Application.Tests/
│   │       └── SG.Api.IntegrationTests/
│   │
│   └── frontend/sg-web/         ← Vite + React + TS + Ant Design
│
├── database/
│   ├── init/                    ← extensions, roles
│   └── seed/                    ← catálogos, usuario admin
│
├── infra/
│   ├── docker/
│   │   ├── docker-compose.yml
│   │   ├── docker-compose.local.yml
│   │   ├── docker-compose.prod.yml
│   │   ├── api.Dockerfile
│   │   ├── web.Dockerfile
│   │   └── caddy/
│   ├── backup/
│   └── tiles/
│
├── data-migration/
│   ├── shapefiles/
│   ├── spreadsheets/
│   └── legacy-postgres/
│
├── docs/
│   ├── arquitectura/
│   ├── dominio/
│   ├── api/
│   ├── gis/
│   ├── operacion/
│   ├── decisiones/              ← ADRs y resúmenes de sprint
│   └── normativa/
│
├── scripts/                     ← setup, start, stop, seed
│
└── .github/workflows/           ← ci.yml, release.yml, codeql.yml
```

---

## 6. Entidades del dominio

### Núcleo principal

- **Predio** (entidad raíz del catastro)
- **Propietario** (persona natural o jurídica)
- **RelacionPredioPropietario** (con historial)
- **UbicacionCatastral**
- **GeometriaPredial** (`geometry(Polygon, 32719)`)
- **Construccion**
- **Colindancia**

### Trámites y certificados

- **Tramite**
- **Mutacion**
- **Certificado**
- **DocumentoAdjunto** (almacenado en MinIO)

### Valuación

- **Valuacion**
- **ZonaHomogenea**
- **Autoavaluo**

### Seguridad y trazabilidad

- **Usuario**
- **Rol**
- **Permiso**
- **Auditoria** (inmutable, generada vía interceptor EF Core)
- **Catalogo** (valores normalizados)

---

## 7. Value Objects obligatorios

Estos NO se modelan como strings libres:

### CodigoCatastral

- Formato Caranavi: `2-04-ZZZ-MMM-LLL` (provisional, validación oficial pendiente)
- Debe tener: validación, parseo, normalización, formato canónico, comparación.
- Aceptar entrada con o sin guiones (`2-04-001-002-003` y `2040010020003`).

Otros VOs a considerar conforme avance: `Cedula`, `NIT`, `CoordenadaUTM`, `Superficie`, `MontoBs`.

---

## 8. Reglas transversales del negocio (críticas)

1. El **código catastral debe ser único**.
2. Todo predio debe tener **ubicación mínima**.
3. Todo predio debe poder vincularse a **geometría**.
4. Toda **modificación relevante genera auditoría** (automática, no manual).
5. La **titularidad debe tener historial**.
6. Los **certificados deben registrarse**.
7. Los **catálogos controlan valores normalizados**.
8. La **valuación registra método y vigencia**.
9. La **geometría almacena SRID**.
10. El sistema soporta **transición entre autoavalúo y catastro formal**.

---

## 9. Modelo de auditoría

Toda operación de escritura (INSERT, UPDATE, DELETE) genera registro automático en `auditoria` vía interceptor de EF Core:

| Campo | Tipo | Descripción |
|---|---|---|
| `id` | guid | PK |
| `timestamp` | timestamptz | Hora UTC |
| `usuario_id` | guid | FK a usuarios |
| `modulo` | varchar | Módulo funcional |
| `accion` | varchar | INSERT/UPDATE/DELETE/LOGIN/EXPORT |
| `entidad_tipo` | varchar | Nombre de la entidad |
| `entidad_id` | varchar | ID afectado |
| `valor_anterior` | jsonb | Estado previo (NULL en INSERT) |
| `valor_nuevo` | jsonb | Estado posterior (NULL en DELETE) |
| `resultado` | varchar | OK / ERROR |
| `ip_origen` | inet | IP de quien hizo la acción |
| `motivo` | text | Opcional, libre |

**El programador de feature NUNCA escribe código de auditoría manualmente.** Sucede automáticamente.

---

## 10. Autenticación y autorización

- **ASP.NET Core Identity + JWT Bearer + BCrypt** (NO el hasher por defecto).
- JWT corto (15 min) + Refresh token (7 días, almacenado en DB, revocable).
- En modo local: usuario `admin` único en seed, password definido en `.env` en primer arranque.
- En modo servidor: registro solo por admin, no autoregistro.

### Roles base

| Rol | Permisos |
|---|---|
| `Admin` | Todo, incluida gestión de usuarios |
| `Tecnico` | CRUD predios, propietarios, geometrías, valuación |
| `Operador` | CRUD predios, propietarios. Sin valuación. |
| `Consulta` | Solo lectura, búsquedas, reportes |

Permisos finos vía claims, no vía roles. Roles agrupan claims.

---

## 11. Reglas operativas para Codex

### Puede

- Crear, modificar, eliminar archivos.
- Reorganizar carpetas dentro de la estructura definida.
- Corregir errores propios y heredados.
- Ejecutar comandos `dotnet`, `npm`, `docker`, `git`, `psql` en el entorno.

### DEBE (obligaciones)

1. **Leer este AGENTS.md al inicio de cada sesión**, sin excepciones.
2. **Antes de crear o modificar archivos**, mostrar el plan de cambios y esperar aprobación del usuario.
3. **Compilar después de cada cambio significativo** y reportar resultado.
4. **No avanzar si hay errores de compilación o tests en rojo**.
5. **Documentar decisiones técnicas** en `docs/decisiones/` como ADR breve.
6. **Al cerrar un sprint**, generar `docs/decisiones/sprint-NN-resumen.md`.
7. **Mantener la estructura oficial del repositorio** (sección 5).
8. **Respetar la separación de capas** (sección 4).
9. **Si encuentra contradicción** entre código existente y este AGENTS.md, **señalarla antes de proceder**.
10. **Si una tarea bloquea por falta de información, preguntar**. NUNCA inventar.

### NO debe

1. **Introducir tecnologías fuera del stack oficial** (sección 3) sin autorización explícita.
2. **Modificar módulos fuera del alcance del sprint actual**.
3. **Duplicar entidades existentes**.
4. **Crear lógica duplicada**.
5. **Mezclar frontend y dominio**.
6. **Mezclar infraestructura y reglas de negocio**.
7. **Ejecutar SQL directo desde la capa de presentación**.
8. **Calcular valuaciones desde la capa de presentación**.
9. **Generar archivos temporales inútiles**.
10. **Avanzar a la siguiente fase si la actual no compila o no pasa pruebas**.

---

## 12. Convenciones de código

### Naming

- **Tablas**: `snake_case` plural → `predios`, `propietarios`, `relaciones_predio_propietario`
- **Columnas**: `snake_case` → `codigo_catastral`, `created_at`
- **Clases C#**: `PascalCase` singular → `Predio`, `Propietario`
- **Métodos C#**: `PascalCase` con verbo → `CrearPredio`, `BuscarPropietarios`
- **Componentes React**: `PascalCase` → `PredioFormulario.tsx`
- **Hooks React**: `usePascalCase` → `usePredios.ts`
- **Endpoints REST**: `kebab-case` plural → `/api/predios`, `/api/relaciones-predio-propietario`

### Idioma del código

- **Nombres de tipos, métodos, variables, tablas, columnas**: español (dominio del negocio).
- **Excepciones**: APIs estándar (`ToString`, `Equals`), nombres técnicos (`Repository`, `Handler`, `Validator`), URLs HTTP.
- **Comentarios y documentación**: español.
- **Mensajes de commit**: español.

### Auditoría obligatoria

Toda entidad de dominio incluye:
```csharp
public DateTime CreatedAt { get; private set; }
public DateTime UpdatedAt { get; private set; }
public Guid CreatedBy { get; private set; }
public Guid? UpdatedBy { get; private set; }
public bool IsDeleted { get; private set; }    // soft delete
```

### Tests

- Cobertura mínima en **dominio**: 80%.
- Cobertura objetivo en **aplicación**: 60%.
- Tests de integración usan **Testcontainers** (PostgreSQL real, no in-memory).

---

## 13. Git y branching

| Rama | Propósito |
|---|---|
| `main` | Solo código probado y tagueado. Nunca commits directos. |
| `develop` | Integración. PRs llegan aquí. |
| `feature/sprint-NN-<descripcion>` | Cada sprint o feature aislada. |
| `hotfix/<descripcion>` | Solo parches urgentes desde `main`. |

### Política de integración y releases

- `develop` es la rama de integración continua. Todo PR de una fase debe tener
  `develop` como base.
- Al crear un PR, verificar **siempre** visualmente que la base sea `develop`
  antes de crearlo y volver a verificarla antes de mergearlo. La rama por
  defecto del repositorio ya es `develop`, pero esa preselección no sustituye
  la verificación.
- `main` representa el último release estable. No recibe PRs de features ni
  commits directos.
- El release del mantenedor único se cosecha con fast-forward desde `develop`,
  se etiqueta sobre `main` y se publica según ADR 0058. Si `--ff-only` falla,
  detenerse y diagnosticar la divergencia.
- Política completa, antecedente de los PR #7 y #12 y evolución futura:
  `docs/decisiones/0058-politica-de-ramas-y-releases.md`.

### Convención de commits

Formato: `<tipo>(<alcance>): <descripción corta>`

Tipos: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `style`, `perf`.

Ejemplos:
- `feat(predio): agregar validación de código catastral único`
- `fix(auth): corregir refresh token expirado retorna 401`
- `docs(arquitectura): documentar capa de aplicación`
- `chore(docker): actualizar postgres a 16.4`

---

## 14. Roadmap por sprints (MVP 21 días + buffer)

| Sprint | Días | Foco | Entregable verificable |
|---|---|---|---|
| 0 | 1-2 | Setup repo, Docker base | `docker compose up` levanta postgres+minio |
| 1 | 3-5 | Backend skeleton, Auth, primera entidad | Login JWT funcional |
| 2 | 6-8 | Dominio: Predio, Propietario, CodigoCatastral | Tests dominio en verde, ≥80% cobertura |
| 3 | 9-11 | API REST CRUD + auditoría | Swagger funcional |
| 4 | 12-14 | Frontend skeleton, login, layout | Login + navegación |
| 5 | 15-17 | UI Predio + Propietario | CRUD end-to-end |
| 6 | 18-19 | Migración de datos de prueba | Datos cargados y validados |
| 7 | 20-21 | Despliegue local + docs operativas | Instalable en otra PC |

**Buffer realista**: +7 a +14 días para refinamiento y edge cases.

---

## 15. Vacíos institucionales pendientes

Estos NO bloquean el desarrollo técnico pero deben resolverse antes de uso real en producción. Hasta entonces, los catálogos respectivos llevan sufijo `_PROVISIONAL` y un campo `requiere_validacion_oficial = true`:

- Validación oficial del formato del código catastral por entidad competente.
- Validación de zonificación oficial.
- Catálogos institucionales aprobados.
- Tablas oficiales de valuación zonal.
- Acuerdos con tesorería municipal sobre integración tributaria.

---

## 16. Normativa de referencia

- **DS 22902/1991** — Reglamento de Catastro Urbano.
- **RM 076/2022** — Resolución Ministerial.
- **RM 024/2024** — Resolución Ministerial.
- **Ley 843** — Reforma Tributaria.
- **Ley 031** — Marco de Autonomías y Descentralización.

La normativa debe convertirse en: reglas, validaciones, estructuras de datos, procesos, criterios de emisión, restricciones, cálculos y trazabilidad. Toda regla normativa implementada se documenta en `docs/normativa/` con su artículo de origen.

---

## 17. Información del entorno del mantenedor

- **Sistema operativo**: Windows 10/11
- **Carpeta del proyecto**: `C:\Proyectos\SG_SAUL_CATASTRO`
- **Terminal preferida**: Git Bash (MINGW64) y PowerShell
- **IDE**: VS Code 1.118+
- **Docker**: Docker Desktop 29.4+
- **.NET SDK**: 10.0.203 (también disponibles 8.0.314, 8.0.411, 9.0.301 — usar SIEMPRE 10)
- **Node.js**: pendiente migrar a 22 LTS (actualmente 25)
- **Git**: 2.54.0
- **Usuario GitHub**: sauronsaul
- **Conexión**: SSH configurada y verificada

---

## 17-bis. Entorno del ejecutor y mecanismos canónicos

- El ejecutor Codex opera en **PowerShell**. Antes de declarar un bloqueo de
  herramientas, debe verificar el mecanismo canónico correspondiente al shell
  actual contra este archivo y el historial de la fase.
- La suite canónica se ejecuta desde la raíz mediante
  `dotnet test src\backend\SG.slnx`. La solución real es `SG.slnx` y no está en
  la raíz; ejecutar `dotnet test` sin ruta desde la raíz falla con `MSB1003`.
  La suite estándar actual contiene 318 pruebas. `SG.Web.E2E` es una suite
  Playwright independiente y no se incluye en ese total.
- El cierre posterior a cambios de paquetes exige un restore fresco con
  `dotnet restore src\backend\SG.slnx`; no se acepta inferir el estado de
  dependencias desde un build ejecutado con `--no-restore`. Después del restore
  aprobado se ejecutan build con `--no-restore` y tests con `--no-build`.
- Antes de reportar una herramienta como rota, verificar en este archivo y en
  el historial de la fase si existe un mecanismo canónico para el shell y el
  entorno propios. El caso de referencia es SQL: PowerShell usa `sql.ps1`, no
  el `sql.sh` destinado al orquestador Git Bash.
- Los hashes de commit se toman de evidencia actual, nunca de memoria. Antes y
  después de cada operación relevante se registra `git rev-parse --short HEAD`;
  toda evidencia debe quedar anclada al hash que realmente estaba en `HEAD`.
  Cuando sea posible, se prefieren `HEAD` y referencias relativas antes que
  reutilizar hashes citados en un prompt o en una sesión anterior.
- El acceso SQL canónico del ejecutor es
  `powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "<SQL>"`.
  `scripts/sql.sh` pertenece al orquestador que opera con Git Bash; no debe
  invocarse mediante el alias `bash` de PowerShell porque resuelve a WSL.
- En sondeos asíncronos desde PowerShell, usar `do { ... } while (...)` cuando
  deba existir al menos una consulta. Un `while` mal inicializado puede omitir
  por completo el sondeo.
- `Invoke-RestMethod` deserializa JSON. Para evidencia del contrato, canalizar
  el resultado a `ConvertTo-Json -Depth <n>`; el formato tabular de PowerShell
  no representa el payload JSON.
- La API local publicada por Caddy se opera desde PowerShell con
  `Invoke-RestMethod` o `Invoke-WebRequest`, usando `http://localhost` como URL
  base. El `Caddyfile.local` declara `auto_https off` y sirve en `:80`; intentar
  HTTPS con `curl.exe` contra el puerto 443 publicado produce el error
  `schannel: failed to receive handshake`.
- Para obtener un token, enviar `POST /api/auth/login` con el JSON
  `{ email, password }` mediante `Invoke-RestMethod` y usar el `accessToken` de
  la respuesta. La contraseña se captura con `Read-Host -AsSecureString` y se
  convierte sólo en memoria para construir la solicitud; nunca se escribe como
  literal en el comando ni se imprime. Este patrón es exclusivo del entorno
  local sobre `http://localhost`, donde Caddy sirve en claro. Contra cualquier
  despliegue remoto es obligatorio HTTPS. La variable con la contraseña en
  claro no se imprime, no se pasa como argumento y no aparece en evidencia
  (ADR 0014/0042).
- El JSON hacia la API nunca se pasa como argumento de `curl.exe`: PowerShell 5
  altera las comillas y el servidor puede recibir un cuerpo inválido, como
  `\u0027p\u0027 is an invalid start of a property name`. Para JSON usar
  `Invoke-RestMethod`; `curl.exe` sí es válido para multipart.
- Una respuesta `202 Accepted` confirma recepción, no finalización. Se conserva
  el identificador retornado y se consulta el endpoint de estado con
  `Invoke-RestMethod` mediante el sondeo `do { ... } while (...)` ya definido,
  hasta alcanzar un estado terminal; nunca se atribuye éxito al `202` aislado.
- Las contrapruebas HTTP se ejecutan con `curl.exe -i`, nunca con `-s`: un token
  vencido combinado con salida silenciosa es indistinguible de una prueba sin
  respuesta.
- Los `NOTICE` de PostGIS emitidos por `ST_IsValid` son evidencia informativa
  de geometrías inválidas, no un fallo del wrapper SQL. El fallo se determina
  por código de salida, excepción o resultado contractual.
- Los commits se crean con `scripts/commit.sh` mediante
  `'C:\Program Files\Git\bin\bash.exe'`. Los archivos de mensaje deben ubicarse
  fuera del repositorio y fuera de `.git/`.
- Falsos positivos de gitleaks: solo excepción inline `gitleaks:allow` con
  justificación en comentario, previa aprobación del planificador con el
  hallazgo redactado. Nunca exclusiones de archivo ni reglas.
- Está prohibido ofuscar, fragmentar o codificar nombres de variables para
  evadir filtros de secretos. Un filtro o guarda activado exige detenerse y
  reportar el bloqueo.
- Todos los scripts `.ps1` del repositorio deben mantenerse en ASCII puro.
- El backup canónico de `sg_catastro` usa `pg_dump` dentro de `sg_postgres`,
  con las credenciales expandidas dentro del contenedor siguiendo el patrón de
  `scripts/sql.sh`; nunca se interpolan ni imprimen secretos desde PowerShell.
  El archivo se escribe fuera del repositorio, bajo
  `C:\Backups\sg_catastro`, y antes de aceptarlo se comprueba que su tamaño sea
  mayor que cero y que `pg_restore --list` pueda listar su contenido.
- Para leer documentación UTF-8 desde PowerShell se establece
  `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8` o se usa Git Bash.
  Una salida con caracteres de árbol como `├` o `│` corruptos es un defecto de
  decodificación de la terminal, no evidencia suficiente de corrupción del
  archivo.
- El Compose local canónico incluye siempre el archivo de entorno y ambos
  archivos Compose:

  ```powershell
  docker compose --env-file <raiz>\.env `
    -f <raiz>\infra\docker\docker-compose.yml `
    -f <raiz>\infra\docker\docker-compose.local.yml <comando> api
  ```

  El nombre del servicio es `api`. Omitir `--env-file` degrada la configuración
  a credenciales en blanco y no constituye una prueba válida del despliegue.
- Desplegar cambios de código requiere reconstruir la imagen del servicio
  `api`; `docker start` solo revive el contenedor con el código viejo.
- Si `docker compose build` falla, un `up` posterior puede revivir la imagen
  anterior. Tras un build correcto se usa `up --force-recreate` con el Compose
  canónico y se verifica la antigüedad del contenedor con `docker ps` antes de
  atribuirle el código nuevo.
- Después de recrear el despliegue se espera la salud real del contenedor; no se
  acepta un `Start-Sleep` fijo como gate:

  ```powershell
  $intentos = 0
  do {
    Start-Sleep -Seconds 3
    $estadoSalud = docker inspect --format "{{.State.Health.Status}}" sg_api 2>$null
    $intentos++
  } until ($estadoSalud -eq "healthy" -or $intentos -ge 40)
  if ($estadoSalud -ne "healthy") {
    throw "sg_api no alcanzo healthy: estado='$estadoSalud' tras $intentos intentos"
  }
  ```

  Toda verificación funcional comienza después de `healthy`. Ejecutarla mientras
  el contenedor está en `starting` produce falsos negativos; el caso de
  referencia fueron perfiles seed reportados como ausentes antes de que el
  seeder terminara. Un bucle sin cota superior cuelga la sesión si el contenedor
  entra en `crash-loop` o no declara `healthcheck`; el límite de intentos
  convierte ese caso en un fallo explícito.
- `numero_version` es el consecutivo interno del dataset y no debe confundirse
  con su etiqueta comercial. Toda línea base operativa se selecciona por
  `estado = 'Activa'`, nunca suponiendo un número de versión.

---

## 17-ter. Frente CAD/DGN

- En Windows, GDAL está disponible mediante QGIS LTR. Los comandos se ejecutan
  desde **OSGeo4W Shell** del menú Inicio, que incorpora GDAL al `PATH`. El
  entorno de Codex no dispone de GDAL; este frente se ejecuta en la máquina del
  orquestador.
- El driver DGN de GDAL lee directamente archivos DGN v7 y expone una sola capa
  llamada `elements`. Los niveles de MicroStation no son capas: se consultan en
  el atributo numérico `Level`; los rótulos se obtienen del atributo `Text`.
- La inspección canónica de estructura y metadatos es:

  ```text
  ogrinfo -al -so "<ruta.dgn>"
  ```

- El censo de elementos por nivel usa el dialecto SQLite:

  ```text
  ogrinfo -q -dialect SQLITE -sql "SELECT Level, COUNT(*) AS cantidad FROM elements GROUP BY Level" "<ruta.dgn>"
  ```

- La conversión base a GeoPackage excluye los niveles 62 y 63 y materializa el
  resultado antes de aplicar controles espaciales:

  ```text
  ogr2ogr -f GPKG "<salida.gpkg>" "<ruta.dgn>" -dialect SQLITE -sql "SELECT * FROM elements WHERE Level NOT IN (62,63)" -nln catastral -a_srs EPSG:32719
  ```

- `-a_srs` asigna el SRS sin reproyectar; sólo es válido porque las coordenadas
  del DGN ya están en UTM 19S. Si el origen estuviera en otra proyección,
  corresponde usar `-s_srs` y `-t_srs`.
- `-spat` se aplica sobre la capa resultado de `-sql`, no sobre la capa origen.
  Combinarlo con una agregación como `COUNT` o `SUM` falla con
  `ERROR 1: Cannot set spatial filter: no geometry field present in layer`.
  Para contar con filtro espacial se materializa primero a GeoPackage y se
  cuenta sobre el resultado.
- Los conteos sobre DGN con cláusula `WHERE` no son confiables: se observaron
  desviaciones cercanas al 1 % respecto de la agregación completa sin filtro,
  en ambos sentidos y sin causa atribuida. El `GROUP BY` sin `WHERE` sí
  reconcilia contra el `Feature Count` del archivo. Toda cifra destinada a un
  informe se obtiene del GeoPackage materializado, nunca del DGN filtrado.
- Los polígonos DGN aparecen como `Type 6` (`shape`) y `Type 14`
  (`complex shape`). Un lector que procesa sólo `Type 6` pierde silenciosamente
  el 38 % de los polígonos; ambos tipos son obligatorios.
- Los textos del DGN usan codificación heredada; se observó `Urbanizaci¾n` en
  lugar de `Urbanizacion`. Deben normalizarse a UTF-8 antes de persistirlos en
  PostgreSQL.
- `MAX(Text)` en una agregación devuelve el máximo alfabético, no una muestra
  representativa. Para inspeccionar contenido se usa
  `SELECT Text ... LIMIT n`, filtrando un solo `Level` por consulta.
- Los mensajes
  `Warning: organizePolygons() received an unexpected geometry` emitidos por
  `ogrinfo` u `ogr2ogr` son informativos y no bloqueantes. Se evalúa el código
  de salida y el producto obtenido, siguiendo el mismo criterio ya documentado
  en 17-bis para los `NOTICE` de `ST_IsValid`.
- En archivos grandes, la salida de `ogrinfo` y `ogr2ogr` puede quedar inundada
  por esos avisos. Se captura una sola vez con `> <ruta.log> 2>&1` y después se
  filtra con `findstr /v /c:"organizePolygons" <ruta.log>`; no se reejecuta la
  operación para cada filtro de evidencia.
- Excluir `Level IN (62,63)` no es suficiente. Se verificaron elementos sueltos
  con coordenadas corruptas en niveles catastrales que, al visualizarlos sobre
  un mapa base, aparecen en Canadá, Patagonia y la Antártida. De los 121.432
  elementos posteriores a excluir los niveles 62 y 63, el bbox municipal
  descartaba 2.668: 2.645 eran elementos colapsados en origen, con todos sus
  vértices cerca de `(0,0)`, pero 7 eran cartografía legítima recortada por
  error. El criterio canónico no usa un bbox municipal: descarta solamente los
  elementos cuya geometría completa cae fuera del rango válido de UTM 19S para
  Bolivia.
- El conteo reproducible de geometrías descartables se ejecuta sobre el
  GeoPackage ya materializado:

  ```text
  ogrinfo -q -dialect SQLITE -sql "SELECT COUNT(*) AS descartables FROM catastral WHERE ST_MaxX(GEOMETRY) < 200000 OR ST_MinX(GEOMETRY) > 900000 OR ST_MaxY(GEOMETRY) < 7000000 OR ST_MinY(GEOMETRY) > 9500000" "<salida.gpkg>"
  ```

  Los límites `200000-900000` en X y `7000000-9500000` en Y corresponden al
  rango plausible de UTM 19S sobre territorio boliviano, no a un municipio. Su
  propósito es descartar geometrías imposibles, no recortar por extensión
  urbana. El filtro se aplica sobre el GeoPackage materializado, nunca sobre el
  DGN, por las limitaciones ya documentadas de `WHERE` y `-spat`.
- `ST_Contains` produce resultados falsos sobre geometrías inválidas. Se
  observaron cuatro polígonos inválidos de `Type 14` que reportaron contener la
  totalidad de los textos del archivo. Todo join espacial debe filtrar por
  `ST_IsValid` o aplicar `ST_MakeValid` previamente.

---

## 18. Recordatorios finales para Codex

- **Eres un colaborador supervisado, no un agente autónomo absoluto.** Saul aprueba antes de cambios estructurales.
- **Prioriza claridad sobre complejidad.** Código aburrido y obvio es preferible a código brillante y oscuro.
- **Cuando dudes entre dos caminos, elige el más simple y mantenible.**
- **Cuando algo no esté en este AGENTS.md, pregunta antes de decidir.**
- **No adelantes funcionalidades** (mapas avanzados, dashboards, IA, apps móviles, consulta pública) hasta que el dominio, la auditoría y la trazabilidad estén sólidos.
- **El orden de prioridad institucional es**:
  1. Infraestructura
  2. Modelo de dominio
  3. Base de datos
  4. Reglas del negocio
  5. Auditoría
  6. API
  7. Frontend
  8. GIS
  9. Certificados
  10. Integraciones futuras

---

**Versión de este documento**: 1.0
**Última actualización**: 7 de mayo de 2026
**Próxima revisión**: al cerrar Sprint 2
