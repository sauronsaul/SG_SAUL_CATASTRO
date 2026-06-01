# ADR 0039 — Rotación obligatoria de secretos tras filtración detectada

**Estado**: Aceptado
**Fecha**: 2026-05-27
**Sprint**: 3
**Commit que aplica la decisión**: bdc390b

---

## Contexto

El password del usuario `sg_postgres` (acceso a la BD del sistema
catastral) se filtró en dos ocasiones durante el desarrollo del
sistema, en ambos casos exponiendo el mismo valor de password:

1. **Sprint 2** — El script auxiliar `sg_audit_diag` contenía el
   password embebido. Detectado y documentado durante el cierre del
   Sprint 2. La rotación del secreto comprometido quedó pendiente
   tras el registro del incidente.

2. **Sprint 3** — El mismo password apareció en una línea de comandos
   compartida (uso de `PGPASSWORD` inline). Detectado durante el
   ciclo de pruebas del Sprint 3.

La segunda detección expuso un valor que ya estaba comprometido desde
la primera. Entre detección inicial y rotación efectiva transcurrió
aproximadamente un sprint, durante el cual el secreto siguió siendo
válido en el entorno local.

## Causa sistémica

El proyecto ya tenía documentado el principio "secreto filtrado se
regenera" en ADR 0018, anterior a la primera filtración. La política
existía como principio declarativo, pero el ciclo "detectar →
documentar → rotar → invalidar" no estaba formalizado como secuencia
obligatoria.

En ausencia de esa formalización, el paso de documentación se ejecutó
como cierre del incidente, mientras que la rotación quedó implícita
como tarea futura. Cuando un ciclo de detección no incluye la
rotación como acción obligatoria en la misma sesión de trabajo, el
secreto comprometido permanece activo por un tiempo no acotado — y
una segunda filtración del mismo secreto se vuelve un escenario
posible.

Este ADR cierra ese hueco de proceso convirtiendo la rotación en
parte integral de la detección, no en una tarea separada.

## Decisión

Se formaliza la regla de rotación obligatoria como invariante de
proceso del proyecto:

1. **Cualquier filtración detectada de un secreto activa la rotación
   en la misma sesión de trabajo en que se detecta.** Documentar la
   filtración sin rotar el secreto no se considera incidente cerrado.

2. **El secreto viejo se valida como inválido antes de cerrar el
   incidente.** En este caso, se verificó con intento de
   autenticación directa contra PostgreSQL — el error `28P01`
   (invalid_password) confirma que el viejo ya no es aceptado.

3. **Eliminación o saneamiento del vector de filtración.** En este
   caso: eliminación del script `sg_audit_diag`, migración a
   `~/.pgpass` para evitar `PGPASSWORD` inline en comandos.

4. **El nuevo secreto cumple ADR 0014** (caracteres permitidos en
   passwords de connection strings: A-Z, a-z, 0-9, `-`, `_`, `.`).
   40 caracteres, generado aleatoriamente.

5. **Cualquier endurecimiento de configuración relacionada se aplica
   en el mismo commit que la rotación.** En este caso, revisión de
   `pg_hba.conf` (la política `scram-sha-256` del ADR 0015 ya estaba
   aplicada; se redujeron los comentarios verbosos que daban pistas
   sobre la topología de acceso).

## Consecuencias

**Positivas:**

- El ADR 0018 pasa de ser principio declarativo a tener un mecanismo
  de ejecución asociado.
- El criterio de cierre de incidente queda objetivo: rotación
  verificada + vector saneado, no solo "documentado".

**Negativas / costos aceptados:**

- Rotar un secreto en medio de un sprint interrumpe el flujo de
  trabajo (actualizar `.env`, reiniciar contenedores, posibles
  fallos transitorios). Se acepta este costo como contrapartida del
  riesgo de mantener un secreto comprometido activo.

## Alternativas descartadas

| Alternativa | Motivo de descarte |
|---|---|
| Solo documentar y rotar al cierre del sprint | Exactamente el patrón que produjo el escenario descrito; permite ventana de exposición de semanas |
| Externalizar gestión de secretos (Vault, AWS Secrets Manager) | Sobreingeniería para el MVP local; relevante a partir de despliegue en producción municipal (Sprint 5+) |
| Hook pre-commit que detecte patrones de password en diff | Útil pero no suficiente: las filtraciones de este caso ocurrieron en script auxiliar y en línea de comandos, no en un commit. La detección reactiva post-uso es el caso que hay que cerrar. Se evaluará como complemento en Sprint 4 |

## Deuda registrada para Sprint 4

- Evaluar incorporación de un hook pre-commit (`git-secrets`,
  `detect-secrets`, o equivalente) que escanee patrones de password
  antes del commit. Cubre un vector distinto al de este ADR (commits
  con secretos en código) pero complementa el principio general.
- Documentar en `CONTRIBUTING.md` o equivalente el procedimiento
  exacto de rotación (qué archivos tocar, qué comandos correr, cómo
  verificar) para que no dependa de memoria.

## Referencias

- ADR 0014 — Caracteres permitidos en passwords de connection strings
- ADR 0015 — pg_hba.conf: scram-sha-256 para todas las conexiones
- ADR 0018 — Protocolo de no-divulgación de secretos
- ADR 0034 — Connection string leak en SgApiFactory (parte del ADR,
  primer incidente Sprint 2)
