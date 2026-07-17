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

## Mensajes PostGIS

`ST_IsValid` puede emitir `NOTICE` con razones y coordenadas de geometrías
inválidas. Son evidencia informativa de O1, no un fallo por sí mismos. El fallo
lo determina el código de salida, una excepción o un estado terminal distinto
de `PreviewListo`.
