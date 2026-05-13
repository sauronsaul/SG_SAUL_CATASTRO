# ADR 0018 — Protocolo de no-divulgación de secretos

**Fecha**: 2026-05-12
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Durante el Checkpoint 1.2, un archivo `.env` con passwords reales fue
visible en pantalla compartida. Los passwords fueron regenerados de inmediato
y los anteriores quedaron quemados sin haber sido usados en ningún sistema
real. No hubo impacto operativo.

El incidente establece la necesidad de un protocolo explícito y permanente
para el manejo de secretos en este proyecto.

## Decisión — 6 reglas permanentes

**Regla 1 — Cero divulgación de contenido de `.env`.**

El contenido literal de `.env` (passwords, tokens, claves JWT, credenciales
MinIO) no se comparte por ningún canal: capturas de pantalla, copia/paste en
chats, pantalla compartida, foros, issues de GitHub, logs, ni mensajes a
herramientas de IA (incluido Claude Code).

**Regla 2 — Pantalla compartida.**

Antes de compartir pantalla, cerrar o minimizar terminales, editores y
gestores de archivos que puedan mostrar `.env` o cualquier archivo con
secretos. Si ocurre accidentalmente → regenerar inmediatamente (ver Regla 4).

**Regla 3 — Verificación sin exposición.**

Para verificar que un secreto tiene el valor correcto, usar scripts que
reporten solo longitudes, hashes o booleanos, nunca el valor literal.

```powershell
# Correcto:
$pass = (Get-Content .env | Where { $_ -match "^POSTGRES_PASSWORD=" }) -replace "^POSTGRES_PASSWORD=",""
Write-Host "Longitud: $($pass.Length) | Solo alfanum: $($pass -match '^[A-Za-z0-9_.\-]+$')"

# Prohibido:
Write-Host $pass   # nunca
```

**Regla 4 — Regeneración inmediata ante cualquier filtración.**

Si un secreto se expone (pantalla, captura, log, chat, herramienta externa),
se regenera de inmediato. Sin "ya da igual", sin "es solo dev", sin esperar
a ver si hay consecuencias. La regeneración es el procedimiento estándar, no
la excepción. Pasos:

1. Generar nuevo password con el conjunto de caracteres permitidos (ADR 0014).
2. Actualizar `.env` con el nuevo valor en TODAS las variables afectadas.
3. Destruir y recrear el volumen de datos si el password de BD cambió.
4. Confirmar que el sistema arranca y autentifica correctamente.
5. Guardar el nuevo password en el gestor de contraseñas.

**Regla 5 — Almacenamiento autorizado de `.env`.**

El archivo `.env` real existe únicamente en:
- El filesystem local de la máquina de desarrollo.
- Un gestor de contraseñas con cifrado end-to-end.

Prohibido almacenarlo en: repositorios (`.gitignore` lo excluye), snippets,
portapapeles persistentes, notas en la nube sin cifrado, o cualquier servicio
de sincronización no cifrado.

**Regla 6 — Claude Code nunca recibe secretos literales.**

Claude Code (y cualquier herramienta de IA) nunca debe pedir ni recibir
el contenido literal de `.env`. Si Claude Code necesita verificar algo,
pide longitudes, hashes o booleanos. Si Claude Code pide un valor literal
de password, es un error de la herramienta — no cumplir la solicitud.

## Consecuencias

**Positivas**:
- El protocolo convierte el incidente en un procedimiento documentado,
  no en un accidente que se repite.
- La regeneración inmediata elimina el riesgo antes de que escale.
- Las reglas son concretas y verificables — no son intenciones vagas.

**Negativas / compromisos**:
- Verificar secretos requiere más pasos (scripts de longitud/hash vs ver el valor).
  Costo pequeño comparado con el riesgo de exposición.
