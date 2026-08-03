param([Parameter(Mandatory=$true)][string]$Sql)
# Acceso SQL autorizado en desarrollo (ver AGENTS.md).
# El SQL viaja por stdin para evitar el mangling de comillas
# PowerShell->nativo. El ejecutor nunca referencia credenciales.
# D1: powershell.exe -File devuelve 0 si el script termina sin exit
# explicito, aunque el comando nativo haya fallado. Se propaga
# LASTEXITCODE de forma expresa. La salida de psql no se captura
# ni se filtra: pasa directo a la consola.
$Sql | docker exec -i sg_postgres bash -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U sg_admin -d sg_catastro -P pager=off -v ON_ERROR_STOP=1 -f -'
$codigo = $LASTEXITCODE
if ($null -eq $codigo) { $codigo = 1 }
exit $codigo
