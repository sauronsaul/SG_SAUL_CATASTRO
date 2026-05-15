# Generador de JWT_SECRET para SG_SAUL_CATASTRO.
# Salida: cadena hexadecimal de 128 caracteres (512 bits)
# criptograficamente segura.
#
# USO:
#   1. Ejecuta: .\scripts\generate-jwt-secret.ps1
#   2. Copia la salida.
#   3. Pega en tu .env como: JWT_SECRET=<valor>
#   4. NUNCA pegues el valor en chats, capturas, logs ni mensajes a IA.
#      Ver ADR 0018.

$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$hex = [BitConverter]::ToString($bytes) -replace '-', ''
Write-Host $hex.ToLower()
