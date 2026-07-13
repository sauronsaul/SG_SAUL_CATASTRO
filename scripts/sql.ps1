param([Parameter(Mandatory=$true)][string]$Sql)
# Acceso SQL autorizado en desarrollo (ver AGENTS.md).
# El SQL viaja por stdin para evitar el mangling de comillas
# PowerShell->nativo. El ejecutor nunca referencia credenciales.
$Sql | docker exec -i sg_postgres bash -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U sg_admin -d sg_catastro -P pager=off -v ON_ERROR_STOP=1 -f -'
