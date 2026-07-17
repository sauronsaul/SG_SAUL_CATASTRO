# Operación de datasets versionados por municipio

## Identificar municipio y estado

Las rutas requieren el geocódigo INE de seis dígitos. No confundirlo con el
segmento provisional de `Catastro:CodigoCatastral`.

```powershell
$baseUrl = "http://localhost"
$headers = @{ Authorization = "Bearer <access-token-de-la-sesion>" }
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/visor/municipios" -Headers $headers |
  ConvertTo-Json -Depth 10
```

La lista contiene sólo municipios con dataset `Activa`. Para consultar uno:

```powershell
Invoke-RestMethod -Method Get `
  -Uri "$baseUrl/api/visor/022001/configuracion" `
  -Headers $headers |
  ConvertTo-Json -Depth 10
```

`Invoke-RestMethod` deserializa JSON. La evidencia legible exige serializar
explícitamente con `ConvertTo-Json`; la tabla PowerShell no es el payload JSON.

Las lecturas del visor admiten los roles `Admin` y `Tecnico`; las transiciones
del ciclo de vida de una versión (`activar` y `descartar`) son exclusivas de
`Admin`.

## Cargar y esperar el preview

```powershell
$jsonCreacion = curl.exe --silent --show-error --fail-with-body `
  -X POST "$baseUrl/api/importaciones/versiones" `
  -H "Authorization: Bearer <access-token-de-la-sesion>" `
  -F "municipio_codigo=022001" `
  -F "paquete=@C:\ruta\dataset.zip"
if ($LASTEXITCODE -ne 0) { throw "La creación de versión falló." }
$respuesta = $jsonCreacion | ConvertFrom-Json
$respuesta | ConvertTo-Json -Depth 10

do {
  Start-Sleep -Seconds 2
  $estado = Invoke-RestMethod -Method Get `
    -Uri "$baseUrl/api/importaciones/versiones/$($respuesta.datasetVersionId)" `
    -Headers $headers
  $estado | ConvertTo-Json -Depth 20
} while ($estado.estado -eq "EnCarga")

if ($estado.estado -ne "PreviewListo") {
  throw "La carga terminó en estado $($estado.estado)."
}
```

`do/while` garantiza al menos una consulta. Un `while` mal inicializado puede
omitir por completo el sondeo.

## Activar

Sólo después de revisar el preview:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "$baseUrl/api/importaciones/versiones/$($respuesta.datasetVersionId)/activar" `
  -Headers $headers |
  ConvertTo-Json -Depth 20
```

La activación archiva la versión activa anterior del mismo municipio. Nunca se
supone `numero_version`; la línea base se selecciona por `estado = 'Activa'`.

## Descartar un preview

Una versión en `PreviewListo` que no se activará se descarta mediante el dominio:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "$baseUrl/api/importaciones/versiones/$($respuesta.datasetVersionId)/descartar" `
  -Headers $headers |
  ConvertTo-Json -Depth 20
```

La operación cambia el estado a `Descartada` y elimina las filas derivadas de
esa versión en las nueve tablas `capa_*`, sin afectar versiones activas ni otros
municipios. Una versión inexistente devuelve `404`; cualquier estado distinto
de `PreviewListo` devuelve `409`, incluida una segunda solicitud de descarte.
Una versión `Activa` nunca se puede descartar.

El paquete fuente se conserva deliberadamente en MinIO, igual que en una carga
`Fallida`. La purga de PostgreSQL y la eliminación de un objeto en MinIO no
pueden formar una transacción atómica; conservarlo mantiene la evidencia para
diagnóstico y evita una limpieza parcial. No se debe borrar manualmente como si
fuera un archivo huérfano.

## Mensajes PostGIS

`ST_IsValid` puede emitir `NOTICE` con razones y coordenadas de geometrías
inválidas. Son evidencia informativa de O1, no un fallo por sí mismos. El fallo
lo determina el código de salida, una excepción o un estado terminal distinto
de `PreviewListo`.
