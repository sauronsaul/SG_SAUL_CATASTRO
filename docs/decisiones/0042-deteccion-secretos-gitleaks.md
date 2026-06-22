# ADR 0042 — Detección de secretos: gitleaks + wrapper de commit + hook pre-push

**Fecha**: 2026-06-10
**Estado**: Aceptado
**Sprint**: 4 — item 8 (deuda técnica de Sprint 3)
**Autor**: Saul Gutierrez

---

## Contexto

Durante el Sprint 3 se identificó que el password de `sg_postgres` quedó expuesto
en la terminal vía `PGPASSWORD` (registrado en `task_security_rotate_sg_postgres_password.md`).
El item 8 de la deuda técnica requería una capa de detección de secretos para prevenir
que credenciales reales ingresen al historial de git.

El objetivo es detectar secretos **antes del commit** (pre-commit) y **antes del push**
(pre-push), con un re-escaneo completo del historial como baseline.

### Postura de exposición del repositorio

El repositorio es **privado** a la fecha (2026-06), con apertura pública futura como
posibilidad declarada por el propietario.

Hacer público un repositorio publica **todo su historial**, no solo el HEAD. Por tanto
la política es tolerancia cero a secretos en el historial completo, no solo en archivos
vigentes. Cualquier secreto que llegue a commitearse requiere:

1. Rotación inmediata de la credencial comprometida.
2. Evaluación de reescritura de historial (`git filter-repo`) **antes** de cualquier
   apertura pública del repositorio.

---

## Decisión 1 — Herramienta: gitleaks y NO git-secrets

Se eligió **gitleaks** sobre `git-secrets` por las siguientes razones:

- `git-secrets` requiere configuración manual de patrones y no tiene reglas por defecto.
  Cada equipo construye su propio set desde cero.
- `gitleaks` incluye un ruleset mantenido por la comunidad que cubre más de 150 tipos de
  secretos (AWS, GCP, JWT, tokens de servicio, etc.) sin configuración inicial.
- `gitleaks` produce reportes en JSON estructurado (auditables), tiene soporte nativo para
  `.gitleaksignore` por fingerprint, y su modo `--staged` / `--pre-commit` encaja
  directamente con el flujo de trabajo del proyecto.
- Binario único sin dependencias de runtime — instalación simple en Windows/Linux/Mac.

---

## Decisión 2 — Wrapper de commit (scripts/commit.sh) y NO hook pre-commit nativo

La convención del proyecto prohíbe trailers `Co-Authored-By` en los commits. Para
garantizarlo, todos los commits se crean con:

```bash
git -c commit.template= -c core.hooksPath=/dev/null commit --no-verify -F <archivo>
```

El flag `--no-verify` **desactiva hooks**, incluyendo `pre-commit`. Por tanto, un hook
`pre-commit` nativo nunca ejecutaría si se respeta la convención anti-trailer.

La solución es un **wrapper de shell** (`scripts/commit.sh`) que:
1. Valida que haya cambios staged.
2. Ejecuta `gitleaks git --staged --redact` antes de commitear.
3. Si hay leaks: aborta con exit 1, sin crear el commit.
4. Si pasa: crea el commit con el comando canónico anti-trailer.
5. Verifica post-commit que el mensaje no contiene `Co-Authored-By`.

Este diseño garantiza que la detección de secretos y la prohibición de trailers coexistan
sin conflicto, ya que el wrapper es la única vía de creación de commits.

---

## Decisión 3 — Regla custom: connection-string-password

El ruleset por defecto de gitleaks **no detecta** passwords embebidos en connection
strings al estilo ADO.NET / Npgsql, como:

```
ConnectionString=Host=localhost;Username=sg_postgres;Password=Fake123ParaPrueba! # gitleaks:allow
```

**Evidencia**: en la prueba de fuego inicial (Sprint 4 / Fase 0), el wrapper dejó
pasar un archivo con esa línea exacta. gitleaks escaneó 81 bytes, reportó
`0 leaks found`, y el commit se ejecutó.

Las reglas por defecto buscan patrones de alta entropía o formatos conocidos
(JWT, API keys de servicios específicos). Una contraseña arbitraria en una
connection string ADO.NET no activa ninguna regla del default ruleset.

Se agregó la regla custom en `.gitleaks.toml`:

```toml
[[rules]]
id = "connection-string-password"
description = "Password embebido en connection string o variable de entorno"
regex = '''(?i)(password|pwd|pgpassword)\s*=\s*[^;\s"'$<%][^;\s"']{3,}'''
keywords = ["password", "pwd", "pgpassword"]

[rules.allowlist]
regexTarget = "line"
regexes = [
  '''(?i)(password|pwd|pgpassword)\s*=\s*(\$\{|\%\(|<|CHANGEME\b)''',
  '''(?i)(password|pwd|pgpassword)\s*=\s*config\['''
]
```

Tras agregar la regla, la prueba de fuego bloqueó el commit (exit 1, 1 leak found).

---

## Decisión 4 — CI con binario directo y NO gitleaks-action

`gitleaks-action` (la GitHub Action oficial) requiere una **license key** para repositorios
que pertenecen a **cuentas de organización** de GitHub. Los repositorios de cuentas
personales no requieren license key, independientemente de si son públicos o privados.

Cita textual del README oficial de `gitleaks/gitleaks-action`:
> "If you are scanning repos that belong to an organization account, you will need to
> obtain a free license key"
> "If you are scanning repos that belong to a personal account, then no license key
> is required."

El proyecto usa una cuenta personal (`sauronsaul`) — sin license key requerida hoy.
Sin embargo, el destino institucional del sistema (despliegue municipal, posible
formalización o transferencia a una entidad estatal) hace probable una migración
futura a cuenta de organización, escenario en el que la Action pasaría a requerir
licencia. Se elige el binario directo por inmunidad a esa transición, consistente
con el criterio de licenciamiento ya aplicado en ADR 0026 (MediatR) y ADR 0040
(FluentAssertions). Costo aceptado: setup de CI levemente más laborioso (step de
descarga del binario) que las 3 líneas de la Action.

Para la Fase 2 (CI), se ejecutará el binario `gitleaks` directamente en el workflow:

```yaml
- name: Detectar secretos
  run: gitleaks git . --config .gitleaks.toml --redact --no-banner
```

El binario se descarga desde la release oficial (`gitleaks/gitleaks/releases`) en el
step de setup, o se instala vía package manager en el runner. Sin costo de licencia.

**Estado actual**: las tres capas están IMPLEMENTADAS Y VERIFICADAS (2026-06-17).
Capa 1 probada en vivo; Capa 2 con bloqueo real demostrado; Capa 3 corriendo en
verde en CI (commit 955bb8b, los 4 jobs en verde).

---

## Diseño de tres capas

```
Capa 1 — Pre-commit (scripts/commit.sh)        [IMPLEMENTADA Y VERIFICADA]
    └── gitleaks git --staged --redact
    └── Bloquea antes de crear el objeto commit

Capa 2 — Pre-push (scripts/hooks/pre-push)     [IMPLEMENTADA Y VERIFICADA]
    └── gitleaks git --log-opts="<rango>" --redact
    └── Bloquea antes de enviar al remoto
    └── Cubre el caso de commits creados fuera del wrapper
    └── ACTIVACIÓN REQUERIDA por operador: git config core.hooksPath scripts/hooks

Capa 3 — CI (GitHub Actions)                   [IMPLEMENTADA Y VERIFICADA]
    └── gitleaks git . --config .gitleaks.toml --redact (binario 8.30.1, linux_x64)
    └── Re-escaneo del historial en cada push/PR a develop/main
    └── Binario directo, sin gitleaks-action (ver Decisión 4)
```

La Capa 2 (una vez activada con core.hooksPath) es una red de seguridad para commits creados por herramientas externas
(IDEs, scripts de migración, etc.) que no pasan por `scripts/commit.sh`.

---

## Allowlist mínimo deliberado

El allowlist de la regla `connection-string-password` es intencionalmente reducido:

| Patrón | Justificación |
|--------|---------------|
| `${...}` / `%(...)` | Variables de entorno en shell y Python |
| `<...` | Placeholders en documentación (ej. `<MISMO_VALOR_QUE_POSTGRES_PASSWORD>`) |
| `CHANGEME` | Valor canónico de placeholder en `.env.example` |
| `config[` | Acceso a IConfiguration en C# — nunca contiene un valor real |

Cualquier allowlist más amplio aumenta el riesgo de dejar pasar un secreto real.

---

## Mecanismo .gitleaksignore por fingerprint

Los falsos positivos en el **historial ya commiteado** no se pueden resolver
modificando los commits (reescritura de historia pública). Se registran en
`.gitleaksignore` usando su fingerprint completo:

```
<commitSHA>:<file>:<ruleID>:<line>
```

Esto suprime el hallazgo en `gitleaks detect` (escaneo de historial git) sin
alterar ningún commit. Los 7 falsos positivos históricos están documentados en
la sección siguiente.

---

## Falsos positivos históricos — clasificación de los 7

Re-escaneo ejecutado en: 2026-06-10. Historial: 86 commits, 1.40 MB.

| # | Fingerprint (commit corto) | Archivo | Línea | Clasificación |
|---|---------------------------|---------|-------|---------------|
| 1 | `a0b8a274` | `IdentitySeeder.cs` | 70 | FALSO POSITIVO — `var tecnicoPassword = config["Tecnico:Password"]` lee de IConfiguration, sin valor |
| 2 | `a0b8a274` | `IdentitySeeder.cs` | 73 | FALSO POSITIVO — nombre de variable bool, sin valor de secreto <!-- gitleaks:allow --> |
| 3 | `000a42f8` | `IdentitySeeder.cs` | 70 | FALSO POSITIVO — igual a #1 (mismo código, commit alternativo en el grafo) |
| 4 | `000a42f8` | `IdentitySeeder.cs` | 73 | FALSO POSITIVO — igual a #2 |
| 5 | `008a8608` | `IdentitySeeder.cs` | 18 | FALSO POSITIVO — `var adminPassword = config["Admin:Password"]` lee de IConfiguration |
| 6 | `b0ba2938` | `0014-connection-string-caracteres-prohibidos.md` | 16 | FALSO POSITIVO — texto documental Markdown, ejemplo de explicación técnica <!-- gitleaks:allow --> |
| 7 | `19c56e5c` | `.env.example` | 66 | FALSO POSITIVO — comentario de documentación en .env.example, StartLine apunta al bloque de comentario <!-- gitleaks:allow --> |

Ninguno contiene un secreto real. Todos son código C# leyendo de IConfiguration,
texto documental, o placeholders estructurales.

---

## Trampas conocidas

### (a) `regexTarget = "line"` es obligatorio en el allowlist

Sin `regexTarget = "line"`, gitleaks aplica el allowlist contra el **grupo 1 del
regex** (el grupo de captura del keyword: `password`, `pwd`, `pgpassword`), no
contra la línea completa ni el match completo.

Para la regla `connection-string-password`, el grupo 1 es simplemente la palabra
`"password"`. Los allowlist regexes como `config\[` o `CHANGEME` nunca coinciden
contra solo la palabra `"password"`, por lo que el allowlist falla silenciosamente:
el hallazgo sigue reportándose aunque la línea debería estar excluida.

**Evidencia**: con el allowlist correcto pero sin `regexTarget = "line"`, 4 supresiones
fallaban (IdentitySeeder.cs líneas 18, 70 en la primera ejecución; .env.example línea 77).
Al agregar `regexTarget = "line"`, los hallazgos desaparecieron en el siguiente scan.

### (b) EXIT codes de gitleaks no deben medirse a través de pipes

```bash
# MAL — el exit code capturado es el del comando tras el pipe (ej. grep, wc):
gitleaks detect | grep "leaks"
echo "EXIT=$?"   # EXIT del grep, no de gitleaks

# BIEN — capturar antes de pipear, o usar PIPESTATUS:
gitleaks detect --no-banner 2>&1
echo "EXIT=$?"

# BIEN — si se necesita pipe:
gitleaks detect --no-banner 2>&1 | tee /tmp/output.txt
echo "EXIT=${PIPESTATUS[0]}"
```

En el desarrollo de este ADR, un `echo "EXIT=$?"` después de un pipe dio `EXIT=0`
cuando gitleaks había retornado exit 1, enmascarando la detección real de leaks.

---

## Consecuencias

- Todo commit nuevo en el repositorio pasa por escaneo de secretos antes de crearse.
- El historial existente está documentado y auditado: 0 secretos reales, 7 falsos
  positivos clasificados.
- La Capa 2 (pre-push) protege contra commits creados fuera del wrapper.
- La Capa 3 (CI) está activa y verificada (verde en CI, commit 955bb8b); re-escanea
  el historial en cada push/PR a develop/main de forma independiente del wrapper.
- `.gitleaks.toml` y `.gitleaksignore` son parte del repositorio y se mantienen
  junto con el código.

---

## Archivos creados / modificados

| Archivo | Acción |
|---------|--------|
| `scripts/commit.sh` | Nuevo — wrapper de commit con escaneo pre-commit |
| `scripts/hooks/pre-push` | Nuevo — hook pre-push con escaneo por rango |
| `.gitleaks.toml` | Nuevo — config con regla custom y allowlist |
| `.gitleaksignore` | Nuevo — 7 fingerprints de falsos positivos históricos |
| `docs/decisiones/0014-connection-string-caracteres-prohibidos.md` | Modificado — `<!-- gitleaks:allow -->` en línea 16 |
| `.env.example` | Modificado — `# gitleaks:allow` en línea 77 (comentario sobre Password=) |
| `src/backend/SG.Infrastructure/DataSeed/IdentitySeeder.cs` | Modificado — `// gitleaks:allow` en línea 73 (bool tienePassword) |

---

*Refs: Sprint 4 item 8, ADR 0038 (rotación de secretos), tarea pendiente task_security_rotate_sg_postgres_password.md*
