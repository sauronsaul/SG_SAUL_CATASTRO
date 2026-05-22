# ADR 0032 — Convención `catalogo_<tipo>` para tablas de catálogo

**Estado**: Aceptado  
**Fecha**: 2026-05-18  
**Sprint**: 2 / Checkpoint 2.1  

---

## Contexto

La migración M003 creó la tabla `dominio.usos_suelo` para almacenar los valores
controlados de uso de suelo. Inmediatamente se detectó que el nombre no seguía
ningún patrón explícito que la identificara como "tabla de catálogo" frente a
una tabla de entidad de negocio.

El dominio catastral tiene varios catálogos similares previstos:
- Uso de suelo
- Tipo de documento
- Tipo de derecho (actualmente enum, en evaluación)
- Zonas homogéneas
- Estados de trámite

Sin una convención uniforme, cada desarrollador nombra el catálogo a su criterio
(`usos_suelo`, `tipos_documento`, `cat_zona`, `catalogo_tramites`), generando
inconsistencia en el esquema.

## Decisión

Toda tabla de catálogo (entidad controlada, administrable, sin ciclo de vida
propio de negocio) llevará el prefijo `catalogo_` en su nombre:

```
dominio.catalogo_uso_suelo
dominio.catalogo_tipo_documento
dominio.catalogo_zona_homogenea
```

La migración M004 renombró `dominio.usos_suelo` → `dominio.catalogo_uso_suelo`
y actualizó la FK y la PK correspondientes.

### Criterios para clasificar una tabla como catálogo

1. Contiene valores de referencia que normalizan un campo de otra entidad.
2. No tiene transiciones de estado de negocio complejas.
3. Puede ampliarse sin migración de datos (solo INSERT de nuevas filas).
4. Su ciclo de vida lo controla el administrador del sistema, no los procesos catastrales.

## Consecuencias

- El esquema `dominio` distingue visualmente entre entidades de negocio
  (`predios`, `propietarios`, `relaciones_predio_propietario`) y datos de
  referencia (`catalogo_*`).
- Las migraciones futuras que agreguen catálogos deben seguir esta convención
  sin excepción.
- El `DomainSeeder` carga los valores de `catalogo_uso_suelo` al iniciar la API;
  este patrón se replica para futuros catálogos.
- Si un catálogo crece hasta requerir aprobación institucional (por ejemplo,
  zonificación oficial), se marca con `requiere_validacion_oficial = true`
  (ver CLAUDE.md sección 15).

## Alternativas descartadas

| Alternativa | Motivo descartado |
|---|---|
| Sin prefijo (`usos_suelo`, `tipos_doc`) | Ambiguo: no distingue catálogos de entidades |
| Prefijo `cat_` (`cat_uso_suelo`) | Demasiado críptico, no auto-descriptivo |
| Schema separado `catalogos.*` | Complejidad de schemas innecesaria para el MVP |
| Enum en C# para todos los catálogos | No ampliable sin recompilación; no administrable por el operador |
