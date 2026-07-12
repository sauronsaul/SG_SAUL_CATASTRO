# Informe de calidad de datos cartográficos — Uyuni v1

**Dirigido a:** Gobierno Autónomo Municipal de Uyuni  
**Fecha:** 12 de julio de 2026  
**Objeto:** Resultado de la recepción, validación e incorporación de las capas SHP municipales

## 1. Resumen ejecutivo

La entrega cartográfica de Uyuni fue incorporada satisfactoriamente al sistema
catastral institucional. Se recibieron y cargaron 35.013 objetos distribuidos
en siete capas:

| Capa | Objetos incorporados |
|---|---:|
| Parcelas | 11.985 |
| Edificaciones | 18.484 |
| Predios no fotografiados | 2.352 |
| Manzanas | 684 |
| Distritos | 6 |
| Zonas | 9 |
| Vías | 1.493 |
| **Total** | **35.013** |

La calidad general de la entrega es muy alta. La capa principal de parcelas,
que sustenta la identificación jurídica y la reconciliación del maestro
catastral, contiene sus 11.985 objetos con geometría, tripletes
`unidad vecinal–manzana–predio` completos y sin duplicados. Los 11.985 predios
del maestro quedaron reconciliados y presentes en la versión activa.

El proceso de validación detectó 60 objetos que requieren corrección en una
próxima entrega: 28 no tienen geometría dibujada y 32 contienen geometrías
defectuosas. El sistema conservó sus atributos e identificadores para que
puedan localizarse y corregirse en origen.

## 2. Alcance y método de revisión

La validación combinó conteos por capa, revisión de claves catastrales,
comprobaciones PostGIS y lectura independiente de los SHP. Para distinguir
objetos realmente no dibujados de geometrías presentes pero defectuosas se
contrastaron dos lectores: GDAL/Fiona y NetTopologySuite (NTS).

Los números de fila de este informe corresponden al orden de los registros en
los archivos SHP recibidos. El contenido se limita a atributos cartográficos e
identificadores técnicos, sin datos personales.

## 3. Hallazgo 1 — Objetos sin geometría dibujada

Se identificaron **28 objetos sin geometría dibujada**: 4 edificaciones y 24
vías. Sus registros alfanuméricos fueron conservados.

### 3.1 Edificaciones sin dibujo

| Fila SHP | Unidad vecinal | Manzana | Predio | Bloque | ID de edificación de origen |
|---:|---:|---:|---:|---:|---:|
| 24 | 1 | 2 | 15 | 99 | 114819 |
| 322 | 1 | 4 | 11 | 99 | 114820 |
| 413 | 1 | 6 | 14 | 99 | 114821 |
| 655 | 1 | 1 | 22 | 99 | 114818 |

### 3.2 Vías sin dibujo

| Fila SHP | Tipo | Nombre | Material |
|---:|---|---|---|
| 76 | CALLE | CALLE URUGUAY | TIERRA |
| 87 | CALLE | CALLE JUNIN | TIERRA |
| 99 | CALLE | JOSÉ EDUARDO PEREZ | TIERRA |
| 104 | CALLE | CALLE COLOMBIA | TIERRA |
| 177 | CALLE | CALLE URUGUAY | TIERRA |
| 235 | CALLE | CALLE LITORAL | TIERRA |
| 289 | CALLE | CALLE LITORAL | TIERRA |
| 390 | CALLE | CALLE CABRERA | TIERRA |
| 525 | CALLE | CALLE PANDO | TIERRA |
| 777 | CALLE | CALLE URUGUAY | TIERRA |
| 782 | CALLE | CALLE LOA | TIERRA |
| 853 | CALLE | CALLE LOA | LOSETA |
| 855 | CALLE | CALLE URUGUAY | ADOQUIN |
| 1125 | CALLE | CALLE URUGUAY | TIERRA |
| 1143 | CALLE | CALLE 01 | TIERRA |
| 1146 | CALLE | CALLE BOLIVAR | TIERRA |
| 1213 | CALLE | CALLE 25 | TIERRA |
| 1261 | CALLE | CALLE 01 | TIERRA |
| 1478 | CALLE | CALLE LITORAL | TIERRA |
| 1484 | CALLE | CALLE BOLIVAR | TIERRA |
| 1488 | Sin dato | Sin nombre | LOSETA |
| 1489 | Sin dato | Sin nombre | LOSETA |
| 1490 | Sin dato | Sin nombre | ADOQUIN |
| 1491 | Sin dato | Sin nombre | ADOQUIN |

Las filas 1488–1491 tampoco incluyen tipo ni nombre de vía; el material sí se
encuentra registrado.

## 4. Hallazgo 2 — Geometrías defectuosas

Se identificaron **32 objetos cuya geometría está dibujada en el SHP, pero es
topológicamente inválida**: 30 edificaciones y 2 manzanas. GDAL/Fiona logra
construirlas y reporta auto-intersecciones o componentes degenerados; NTS las
rechaza durante el parseo. El sistema conservó sus atributos y las documentó
para revisión.

### 4.1 Edificaciones con geometría defectuosa

| Fila SHP | Unidad vecinal | Manzana | Predio | Bloque | ID de edificación de origen |
|---:|---:|---:|---:|---:|---:|
| 1201 | 1 | 6 | 20 | 1 | 111011 |
| 3350 | 1 | 43 | 9 | 1 | 113342 |
| 3655 | 1 | 49 | 7 | 1 | 113674 |
| 3680 | 1 | 49 | 16 | 1 | 113696 |
| 3683 | 1 | 49 | 16 | 3 | 113698 |
| 4071 | 1 | 1 | 13 | 1 | 110292 |
| 4204 | 1 | 1 | 13 | 1 | 110293 |
| 4443 | 1 | 59 | 13 | 1 | 114519 |
| 4444 | 1 | 59 | 13 | 1 | 114520 |
| 4445 | 1 | 59 | 14 | 1 | 114521 |
| 4460 | 1 | 59 | 21 | 1 | 114538 |
| 4462 | 1 | 59 | 22 | 1 | 114540 |
| 5077 | 1 | 73 | 9 | 2 | 115215 |
| 5078 | 1 | 73 | 9 | 2 | 115216 |
| 6361 | 1 | 96 | 8 | 1 | 116612 |
| 6823 | 2 | 6 | 16 | 1 | 117092 |
| 8487 | 2 | 49 | 24 | 1 | 118938 |
| 8940 | 2 | 68 | 2 | 1 | 119462 |
| 10414 | 2 | 110 | 13 | 1 | 121110 |
| 11228 | 2 | 150 | 6 | 3 | 122035 |
| 12196 | 3 | 12 | 10 | 1 | 123095 |
| 12711 | 3 | 75 | 4 | 1 | 123660 |
| 12716 | 3 | 75 | 13 | 1 | 123665 |
| 15169 | 5 | 59 | 13 | 5 | 126391 |
| 15535 | 5 | 87 | 22 | 1 | 126795 |
| 15538 | 5 | 87 | 23 | 1 | 126798 |
| 15548 | 5 | 88 | 7 | 1 | 126807 |
| 15560 | 5 | 88 | 13 | 1 | 126819 |
| 17811 | 1 | 7 | 6 | 1 | 111037 |
| 18020 | 2 | 49 | 21 | 2 | 118931 |

### 4.2 Manzanas con geometría defectuosa

| Fila SHP | Unidad vecinal | Manzana | Código geográfico | Diagnóstico independiente |
|---:|---:|---:|---|---|
| 125 | 1 | 96 | 04-12-05-01 | Multipolígono con componente de muy pocos puntos |
| 164 | 2 | 38 | 04-12-05-01 | Auto-intersección de anillo |

## 5. Recomendaciones al GAM Uyuni

1. Corregir los 28 objetos sin dibujo en la herramienta CAD/GIS de origen,
   utilizando las filas e identificadores de este informe.
2. Revisar y reconstruir los anillos de las 30 edificaciones y 2 manzanas con
   geometría defectuosa, evitando auto-intersecciones y componentes
   degenerados.
3. Completar el tipo y nombre de las vías correspondientes a las filas
   1488–1491.
4. Reenviar las siete capas como un paquete completo en la próxima entrega,
   conservando los mismos campos identificatorios para permitir una nueva
   comparación versionada.

Las correcciones deben realizarse en los archivos fuente institucionales. El
sistema no altera silenciosamente las geometrías recibidas: conserva la
entrega original, registra las observaciones y permite incorporar una nueva
versión cuando el GAM remita los datos corregidos.

## 6. Estado de incorporación

La versión Uyuni v1 se encuentra activa y operativa. Las observaciones de este
informe no afectan la capa de parcelas ni la reconciliación de los 11.985
predios. Los objetos observados permanecen identificados y trazables para su
corrección en una siguiente entrega.

Este resultado demuestra que el sistema puede recibir, validar, versionar y
auditar entregas cartográficas municipales sin perder los datos originales ni
ocultar sus incidencias de calidad.
