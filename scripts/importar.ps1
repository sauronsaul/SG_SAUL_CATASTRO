param(
  [Parameter(Mandatory=$true)][ValidateSet('importar','estado','activar')][string]$Accion,
  [string]$Zip,
  [string]$Id
)
# Flujo de importacion autenticado - SOLO ORQUESTADOR (ver AGENTS.md).
# Credenciales interactivas; token solo en memoria de esta ejecucion.
# ADVERTENCIA: mantener este archivo en ASCII puro (sin tildes ni
# guiones largos) - PowerShell 5.1 lee sin BOM como ANSI y los
# caracteres multibyte rompen el parseo.
$base = 'http://localhost:5000/api'

$email = Read-Host 'Email'
$sec   = Read-Host 'Password' -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
         [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" `
         -ContentType 'application/json' `
         -Body (@{ email = $email; password = $plain } | ConvertTo-Json)
$plain = $null
$token = $login.accessToken
Write-Host "Login OK. Token expira: $($login.expiresAt)"

switch ($Accion) {
  'importar' {
    if (-not $Zip) { throw 'Falta -Zip' }
    $r = curl.exe -s -X POST "$base/importaciones/versiones" `
         -H "Authorization: Bearer $token" `
         -F "paquete=@$Zip" -w "`nHTTP %{http_code}`n"
    Write-Host $r
  }
  'estado' {
    if (-not $Id) { throw 'Falta -Id' }
    Invoke-RestMethod -Uri "$base/importaciones/versiones/$Id" `
      -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json -Depth 6
  }
  'activar' {
    if (-not $Id) { throw 'Falta -Id' }
    Write-Host "VAS A ACTIVAR $Id - acto irreversible de estado productivo."
    if ((Read-Host 'Escribe ACTIVAR para confirmar') -ne 'ACTIVAR') { throw 'Cancelado.' }
    Invoke-RestMethod -Method Post -Uri "$base/importaciones/versiones/$Id/activar" `
      -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json -Depth 6
  }
}
