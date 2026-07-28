param(
  [Parameter(Mandatory=$true)][ValidateSet('importar','estado','activar','descartar')][string]$Accion,
  [string]$Zip,
  [string]$Id,
  [string]$MunicipioCodigo,
  [string]$Base = 'http://localhost/api'
)
# Flujo de importacion autenticado - SOLO ORQUESTADOR (ver AGENTS.md).
# Credenciales interactivas; token solo en memoria de esta ejecucion.
# ADVERTENCIA: mantener este archivo en ASCII puro (sin tildes ni
# guiones largos) - PowerShell 5.1 lee sin BOM como ANSI y los
# caracteres multibyte rompen el parseo.
#
# ADR 0060: el endpoint exige el campo multipart municipio_codigo.
# ADR 0063: cada paquete es un snapshot municipal completo del esquema
# declarado para ese municipio.

$ErrorActionPreference = 'Stop'

if ($Accion -eq 'importar') {
  if (-not $Zip) { throw 'Falta -Zip' }
  if (-not $MunicipioCodigo) { throw 'Falta -MunicipioCodigo (codigo INE de seis digitos)' }
  if (-not (Test-Path -LiteralPath $Zip)) { throw "No existe el ZIP: $Zip" }
  if ($MunicipioCodigo -notmatch '^\d{6}$') { throw 'El municipio debe ser INE de seis digitos.' }
  $Zip = (Resolve-Path -LiteralPath $Zip).Path
}
if ($Accion -in @('estado','activar','descartar') -and -not $Id) { throw 'Falta -Id' }

$email = Read-Host 'Email'
$sec   = Read-Host 'Password' -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
         [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
$login = Invoke-RestMethod -Method Post -Uri "$Base/auth/login" `
         -ContentType 'application/json' `
         -Body (@{ email = $email; password = $plain } | ConvertTo-Json)
$plain = $null
$token = $login.accessToken
if (-not $token) { throw 'Login sin accessToken en la respuesta.' }
Write-Host "Login OK contra $Base. Token expira: $($login.expiresAt)"

$auth = @{ Authorization = "Bearer $token" }

switch ($Accion) {

  'importar' {
    Write-Host "Subiendo $Zip para municipio $MunicipioCodigo ..."
    $r = curl.exe -s -X POST "$Base/importaciones/versiones" `
         -H "Authorization: Bearer $token" `
         -F "municipio_codigo=$MunicipioCodigo" `
         -F "paquete=@$Zip" `
         -w "`nHTTP %{http_code}`n"
    Write-Host $r
    Write-Host 'Si el codigo es 202, use -Accion estado -Id <datasetVersionId> hasta PreviewListo.'
  }

  'estado' {
    Invoke-RestMethod -Uri "$Base/importaciones/versiones/$Id" -Headers $auth |
      ConvertTo-Json -Depth 6
  }

  'activar' {
    Write-Host "VAS A ACTIVAR $Id."
    Write-Host 'La version activa actual de ese municipio pasara a Archivada (ADR 0063).'
    Write-Host 'Es reversible: una version Archivada puede reactivarse con esta misma accion.'
    if ((Read-Host 'Escribe ACTIVAR para confirmar') -ne 'ACTIVAR') { throw 'Cancelado.' }
    Invoke-RestMethod -Method Post -Uri "$Base/importaciones/versiones/$Id/activar" -Headers $auth |
      ConvertTo-Json -Depth 6
  }

  'descartar' {
    Write-Host "VAS A DESCARTAR $Id."
    Write-Host 'Purga sus filas capa_* de forma irreversible. Solo aplica a PreviewListo.'
    Write-Host 'El objeto del paquete permanece en MinIO (deuda registrada, ADR 0035).'
    if ((Read-Host 'Escribe DESCARTAR para confirmar') -ne 'DESCARTAR') { throw 'Cancelado.' }
    Invoke-RestMethod -Method Post -Uri "$Base/importaciones/versiones/$Id/descartar" -Headers $auth |
      ConvertTo-Json -Depth 6
  }
}
