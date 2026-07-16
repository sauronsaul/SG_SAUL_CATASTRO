# Informe de calidad de datos cartográficos — Uyuni v2: hallazgos incrementales

**Dirigido a:** Gobierno Autónomo Municipal de Uyuni

**Fecha:** 16 de julio de 2026

**Estado:** insumo técnico para la segunda edición del informe

**Fuente:** dataset Uyuni con `estado = 'Activa'`, versión interna 3

> La denominación “v2” corresponde a la edición del informe, no al
> `numero_version` interno del dataset. La línea base operativa se seleccionó
> por estado `Activa`.

## 1. Alcance

La verificación visual del croquis predial reveló una grafía dudosa en el campo
de barrio. Se contrastó el valor con la totalidad de parcelas activas y se
extendió el análisis a los campos de texto libre `direccion_barrio` y
`nombre_via`.

El sistema no corrige ni homogeneiza estos valores. Los conteos se comunican
para que el GAM valide y, si corresponde, corrija los archivos fuente en una
nueva entrega versionada.

## 2. Variantes crudas relacionadas con Inmaculada Concepción

En `direccion_barrio` existen seis grafías que contienen `CONCEP`. Se preservan
aquí exactamente como están almacenadas:

| Valor crudo | Registros |
|---|---:|
| `INMACULADA CONCEPCION` | 86 |
| `INMATULADA CONCEPCION` | 29 |
| `INMACULADA CONCEPCIÓN` | 15 |
| `ZONA INMACULADA CONCEPCIÓN` | 2 |
| `IMMACULADA CONCEPCION` | 1 |
| `INMUTILADA CONCEPCION` | 1 |
| **Total del conjunto observado** | **134** |

`INMUTILADA CONCEPCION` corresponde a la fila de origen `11455`, triplete
`1/2/1`. La revisión de su manzana mostró que los otros 18 predios usan
`INMATULADA CONCEPCION`, no una grafía uniforme que el sistema pueda asumir
como corrección. El GAM debe determinar la denominación institucional válida.

También existe un registro de `nombre_via = 'INMACULADA CONCEPCIÓN'`; no se
mezcla con los conteos de barrio porque pertenece a otro campo.

## 3. Normalización básica de barrio y vía

Se aplicó únicamente `upper + trim + eliminación de tildes` para medir
variaciones tipográficas simples. No se corrigieron espacios internos, errores
ortográficos, abreviaturas ni equivalencias semánticas.

| Campo | Registros no vacíos | Valores crudos distintos | Claves normalizadas distintas | Claves con más de una variante cruda | Registros dentro de esas claves |
|---|---:|---:|---:|---:|---:|
| Barrio (`direccion_barrio`) | 11.668 | 189 | 180 | 9 | 8.158 |
| Vía (`nombre_via`) | 11.728 | 2.100 | 2.097 | 3 | 139 |

### 3.1 Grupos de barrio que colapsan por normalización básica

| Clave normalizada | Registros | Variantes crudas y conteos |
|---|---:|---|
| `PROGRESO` | 1.962 | `PROGRESO` (1.957), `progreso` (5) |
| `ANDES` | 1.225 | `ANDES` (1.222), `andes` (3) |
| `MIRAFLORES` | 1.161 | `MIRAFLORES` (1.156), `miraflores` (5) |
| `CAMPERO` | 1.140 | `CAMPERO` (1.138), `campero` (2) |
| `LINDO` | 760 | `LINDO` (759), `lindo` (1) |
| `SUD` | 710 | `SUD` (709), `sud` (1) |
| `11 DE JULIO` | 592 | `11 DE JULIO` (591), `11 de JULIO` (1) |
| `NUEVO CENTENARIO` | 368 | `NUEVO CENTENARIO` (367), `nuevo centenario` (1) |
| `CENTENARIO` | 240 | `CENTENARIO` (239), `CENTENArio` (1) |

### 3.2 Grupos de vía que colapsan por normalización básica

| Clave normalizada | Registros | Variantes crudas y conteos |
|---|---:|---|
| `SIN NOMBRE` | 113 | `SIN NOMBRE` (112), `sin nombre` (1) |
| `OCTAVIO` | 21 | `OCTAVIO` (20), `octavio` (1) |
| `JOSEFINA` | 5 | `JOSEFINA` (4), `josefina` (1) |

La repetición exacta de un nombre de vía no implica por sí sola un error: una
misma vía puede corresponder a múltiples parcelas u objetos. Por ejemplo, el
informe v1 ya registró cinco objetos sin geometría llamados `CALLE URUGUAY`.
Este análisis busca variantes de escritura, no deduplicar entidades viales.

## 4. Implicación de calidad y diseño

Los campos de barrio y vía llegan como texto libre sin catálogo controlado en
el origen. Esto permite diferencias de mayúsculas y tildes, pero también
variantes que una normalización mecánica no puede resolver, como
`INMUTILADA`, `INMATULADA`, `IMMACULADA` e `INMACULADA`.

El hallazgo produce dos insumos:

1. **Para el GAM:** validar las seis grafías y comunicar la denominación
   institucional correcta; corregirla en la fuente y remitir una nueva versión.
2. **Para la futura fase de edición:** diseñar catálogos controlados de barrios
   y vías, con identificador estable, denominación oficial, alias de búsqueda e
   historial. La UI no debe convertir silenciosamente texto legado a un
   catálogo sin una decisión institucional y una migración trazable.

## 5. Consulta reproducible

```sql
WITH activa AS (
    SELECT id, numero_version
    FROM dominio.dataset_versiones
    WHERE municipio_codigo = 'UYUNI' AND estado = 'Activa'
), textos AS (
    SELECT a.numero_version, x.campo, btrim(x.valor) AS valor_crudo,
           translate(
               upper(btrim(x.valor)),
               'ÁÉÍÓÚÜÑÀÈÌÒÙÂÊÎÔÛÄËÏÖÜ',
               'AEIOUUNAEIOUAEIOUAEIOU') AS clave_normalizada
    FROM activa a
    JOIN dominio.capa_parcelas cp ON cp.dataset_version_id = a.id
    CROSS JOIN LATERAL (VALUES
        ('barrio', cp.direccion_barrio),
        ('via', cp.nombre_via)
    ) AS x(campo, valor)
    WHERE nullif(btrim(x.valor), '') IS NOT NULL
)
SELECT numero_version, campo, clave_normalizada, valor_crudo, count(*)
FROM textos
GROUP BY numero_version, campo, clave_normalizada, valor_crudo
ORDER BY campo, clave_normalizada, valor_crudo;
```

La consulta se ejecutó mediante el wrapper canónico `scripts/sql.ps1`. No se
ejecutó ninguna sentencia de modificación.
