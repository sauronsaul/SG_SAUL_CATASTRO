# Verificación E2E del visor con Playwright

Este procedimiento ejecuta el navegador Chromium real contra el stack Compose
ya desplegado. Verifica el mapa, la búsqueda T-2.3, la ficha T-2.4 y la vista
imprimible del croquis T-2.5. El proyecto
`src/frontend/tests/SG.Web.E2E` queda deliberadamente fuera de
`src/backend/SG.slnx`: la suite estándar no descarga navegadores ni requiere
credenciales.

## 1. Verificar y desplegar el stack

Desde PowerShell, en la raíz del repositorio:

```powershell
docker compose --env-file "$PWD\.env" `
  -f "$PWD\infra\docker\docker-compose.yml" `
  -f "$PWD\infra\docker\docker-compose.local.yml" ps
```

`postgres`, `minio`, `api` y `web` deben aparecer `healthy`; `caddy`,
`running`. T-2.5 modifica API y frontend, por lo que ambos servicios deben
reconstruirse antes de probar. `docker start` no despliega código nuevo:

```powershell
docker compose --env-file "$PWD\.env" `
  -f "$PWD\infra\docker\docker-compose.yml" `
  -f "$PWD\infra\docker\docker-compose.local.yml" `
  up -d --build api web
```

## 2. Compilar el proyecto independiente

```powershell
dotnet build src\frontend\tests\SG.Web.E2E\SG.Web.E2E.csproj `
  --configuration Debug
```

El build crea
`src\frontend\tests\SG.Web.E2E\bin\Debug\net10.0\playwright.ps1`.

## 3. Instalar Chromium

La versión fijada es `Microsoft.Playwright 1.61.0`. Cada versión requiere sus
propios binarios de navegador. Instalar sólo Chromium:

```powershell
powershell -ExecutionPolicy Bypass `
  -File src\frontend\tests\SG.Web.E2E\bin\Debug\net10.0\playwright.ps1 `
  install chromium
```

La descarga usa el CDN de Microsoft y queda en
`%USERPROFILE%\AppData\Local\ms-playwright`. Si el municipio usa proxy, definir
`HTTPS_PROXY` en la sesión antes del comando; si usa un repositorio interno,
definir `PLAYWRIGHT_DOWNLOAD_HOST`. No guardar esos valores en Git.

## 4. Configurar la ejecución sin exponer credenciales

Definir las credenciales de un usuario `Admin` o `Tecnico` únicamente en la
sesión PowerShell que ejecutará la prueba:

```powershell
$env:SG_E2E_BASE_URL = "http://localhost"
$env:SG_E2E_EMAIL = "<correo-configurado-por-el-orquestador>"
$env:SG_E2E_PASSWORD = "<contraseña-configurada-por-el-orquestador>"
$env:SG_E2E_ARTIFACTS = "$PWD\artifacts\e2e"
```

No imprimir estas variables, no volcarlas a archivos y no incluir Request
Payload ni encabezados `Authorization` en la evidencia.

## 5. Ejecutar el flujo completo

```powershell
dotnet test src\frontend\tests\SG.Web.E2E\SG.Web.E2E.csproj `
  --configuration Debug --no-build `
  --logger "console;verbosity=detailed"
```

Resultado esperado:

- login real y navegación a `/visor`;
- al menos una respuesta HTTP 200 bajo `/api/tiles/`;
- zoom inicial de la instancia MapLibre mayor que 10;
- búsqueda del triplete fijo `1/1/1` mediante los campos **Distrito**,
  **Manzana** y **Predio**;
- respuesta 200 de
  `/api/predios/buscar?distrito=1&manzana=1&predio=1`;
- encuadre del predio con zoom mayor que 17;
- resaltado visible mediante relleno amarillo translúcido y contorno rojo
  grueso, filtrado exactamente por el triplete `1/1/1`;
- persistencia del filtro al bajar de zoom 15 y volver a zoom mayor que 17;
- panel **Ficha del predio** visible con fila `11883`, código geográfico
  `04-12-05-01`, estado `Importado`, superficies declarada `238,3470 m²` y
  gráfica `238,3466 m²`, tipo `VIV`, vía `COLON Y SUCRE` y versión interna 3;
- botón **Imprimir croquis** abre **Vista previa del croquis** sin perder la
  sesión ni abrir una segunda pestaña;
- membrete `GOBIERNO AUTÓNOMO MUNICIPAL DE UYUNI`, triplete `1 / 1 / 1`, fecha
  con sufijo `UTC-4`, datos esenciales, versión interna 3 y advertencia de que
  no constituye certificado oficial;
- SVG con `data-srid="32719"`, trayectoria Polygon, una barra de escala gráfica
  rotulada en metros y flecha `N - UTM 19S`;
- captura `croquis-simple-*.png` y retorno a la ficha mediante
  **Volver a la ficha**;
- cierre del panel, clic en la parcela encuadrada y segunda respuesta 200 del
  mismo endpoint, con la ficha nuevamente visible y el mismo resaltado;
- limpieza del filtro de resaltado al cerrar la ficha;
- una captura `visor-minimo-*.png` en `artifacts/e2e`;
- `Total: 1, Failed: 0, Passed: 1`.

Criterio de fallo: variable obligatoria ausente, login rechazado, falta de tile
200 en 30 segundos, mapa no visible, zoom inicial menor o igual a 10, búsqueda
sin 200, zoom al predio menor o igual a 17, campo distinto del valor esperado,
panel ausente después de la búsqueda o el clic, filtro de resaltado incorrecto
o persistente tras cerrar, croquis sin SRID 32719, barra gráfica, norte de
cuadrícula, fecha UTC-4 o datos esperados, ausencia de cualquiera de las dos
capturas o cualquier prueba roja.

## 6. Verificación humana obligatoria de impresión

El E2E comprueba el contenido de la vista, pero no abre ni juzga el diálogo
nativo de impresión. Con el stack desplegado y la sesión autenticada:

1. buscar el predio `1/1/1` y abrir **Imprimir croquis**;
2. pulsar **Imprimir**;
3. imprimir una hoja física o elegir **Guardar como PDF** en el navegador;
4. usar A4 horizontal, escala del navegador `100 %` y sin encabezados ni pies
   automáticos del navegador;
5. verificar visualmente que:
   - el documento ocupa una sola página y no muestra barra superior, mapa,
     panel de capas ni botones de la aplicación;
   - membrete, triplete, códigos, propietario de referencia, ubicación,
     superficies, versión y advertencia son legibles;
   - la geometría está completa, mantiene proporción y no queda recortada;
   - la flecha `N - UTM 19S` y su rótulo son legibles;
   - la barra gráfica está dentro del SVG, tiene extremos visibles y su valor en
     metros puede leerse; la referencia `1:N` aparece sólo como dato secundario;
   - la fecha termina en `UTC-4` y corresponde a la fecha/hora boliviana;
6. registrar en la evidencia: navegador y versión, destino papel/PDF, tamaño,
   orientación, escala seleccionada y resultado de cada punto anterior.

El PDF o escaneo puede conservarse como artefacto del orquestador, pero no debe
commitearse: contiene datos del predio y propietario de referencia. Cualquier
recorte, segunda página, barra ilegible o sustitución por norte geográfico falla
el cierre aunque el E2E automatizado esté verde.

## 7. Contrastar la ficha y la geometría con persistencia

Ejecutar mediante el wrapper SQL canónico:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql @'
SELECT cp.cod_uv, cp.cod_man, cp.cod_pred, cp.fila_origen,
       p.codigo_catastral, cp.codigo_geografico, p.estado,
       cp.superficie AS superficie_declarada_m2,
       ROUND(ST_Area(cp.geometria)::numeric, 4) AS superficie_grafica_m2,
       p.superficie_oficial, cp.tipo_inmueble, cp.nombre_via,
       cp.direccion_barrio, cp.direccion_urbana, cp.uso_terreno,
       cp.topografia_terreno, cp.servicio_agua, cp.servicio_luz,
       cp.servicio_alcantarillado, cp.servicio_telefonia,
       ST_SRID(cp.geometria) AS geometria_srid,
       GeometryType(cp.geometria) AS geometria_tipo,
       ST_NPoints(cp.geometria) AS geometria_puntos,
       v.numero_version
FROM dominio.capa_parcelas cp
JOIN dominio.dataset_versiones v ON v.id = cp.dataset_version_id
JOIN dominio.predios p
  ON p.cod_uv = cp.cod_uv
 AND p.cod_man = cp.cod_man
 AND p.cod_pred = cp.cod_pred
 AND p.presente_en_version_activa
 AND NOT p.is_deleted
WHERE v.estado = 'Activa'
  AND cp.cod_uv = 1 AND cp.cod_man = 1 AND cp.cod_pred = 1;
'@
```

Debe devolver una sola fila y cuadrar campo por campo con los asserts del paso
5, incluido `geometria_srid = 32719` y `geometria_tipo = 'POLYGON'`. La línea
base se selecciona por `estado = 'Activa'`, no por una versión supuesta.
Cualquier diferencia entre SQL y la ficha falla el cierre aunque el panel sea
visible.

## 8. Reproducir la evidencia de convergencia UTM

Ejecutar con el wrapper canónico la consulta registrada en ADR 0057. El
resultado de Uyuni v3 activa debe incluir:

```text
azimut_norte_cuadricula_grados=359.23967358
convergencia_grados=-0.76032642
desviacion_lateral_en_20m=0.2654
```

La interpretación aprobada es contextual: `26,5 cm` sobre `20 m` es irrelevante
para este croquis simple, pero no autoriza a describir la convergencia como
despreciable en un documento con pretensión topográfica. Fase 4 debe heredar la
restricción.

## 9. Prueba de sensibilidad ante regresión

Para demostrar que T0 detecta el defecto de 2.C sin modificar el producto,
Playwright puede simular una regresión de transporte abortando todas las rutas
`/api/tiles/`. Se reduce el timeout sólo para esta prueba negativa:

```powershell
$env:SG_E2E_SIMULAR_REGRESION_TILES = "1"
$env:SG_E2E_TILE_TIMEOUT_SECONDS = "5"
dotnet test src\frontend\tests\SG.Web.E2E\SG.Web.E2E.csproj `
  --configuration Debug --no-build `
  --logger "console;verbosity=detailed"
```

El mismo test debe quedar rojo por timeout esperando un tile 200. Esto reproduce
la condición observable de la regresión original: el mapa nunca obtiene un MVT
válido aunque login, navegación e inicialización hayan ocurrido.

Eliminar ambas variables y repetir el paso 5:

```powershell
Remove-Item Env:SG_E2E_SIMULAR_REGRESION_TILES,Env:SG_E2E_TILE_TIMEOUT_SECONDS
```

El mismo binario debe regresar a verde contra el stack sin interceptar. Ninguna
regresión temporal entra al código del visor ni se commitea.
