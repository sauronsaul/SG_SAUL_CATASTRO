# ADR 0049 — Importación por reemplazo completo versionado de las 7 capas SHP (DatasetVersion)

**Fecha**: 2026-07-10
**Estado**: Aceptado
**Autores**: Saul Gutierrez + equipo del proyecto

---

## Contexto

La cartografía catastral se recibe como siete capas SHP relacionadas. Reemplazos
parciales impiden reconstruir qué conjunto de datos originó una consulta y
pueden mezclar capas de fechas distintas.

## Decisión

Cada importación aceptada crea una `DatasetVersion` completa N+1 con las siete
capas. Las versiones previas son inmutables y las consultas operativas se
resuelven sobre la versión activa.

Los datos generados por el sistema —trámites, certificados, valuaciones y
auditoría— viven fuera del versionado cartográfico. Se vinculan con la
cartografía mediante el triplete canónico `(cod_uv, cod_man, cod_pred)`.

## Justificación

Una versión completa da consistencia temporal, trazabilidad de la fuente y una
reversión clara de la versión activa. Separar datos operativos evita que una
actualización cartográfica reescriba evidencia institucional.

## Consecuencias

- No se admiten actualizaciones parciales de una capa dentro de una versión.
- Se conserva almacenamiento histórico para cada versión publicada.
- Los módulos futuros deben consultar explícitamente la versión activa o una
  versión histórica solicitada.
- La implementación del modelo `DatasetVersion` queda para la fase de
  importación; este ADR no crea migraciones ni modifica el dominio actual.
