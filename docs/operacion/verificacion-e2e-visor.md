# Verificación E2E del visor con Playwright

Este procedimiento ejecuta el navegador Chromium real contra el stack Compose
ya desplegado. El proyecto `src/frontend/tests/SG.Web.E2E` queda deliberadamente
fuera de `src/backend/SG.slnx`: la suite estándar no descarga navegadores, no
requiere credenciales y conserva sus 261 pruebas.

## 1. Verificar y desplegar el stack

Desde PowerShell, en la raíz del repositorio:

```powershell
docker compose --env-file "$PWD\.env" `
  -f "$PWD\infra\docker\docker-compose.yml" `
  -f "$PWD\infra\docker\docker-compose.local.yml" ps
```

`postgres`, `minio`, `api` y `web` deben aparecer `healthy`; `caddy`,
`running`. Si se modificó el frontend, reconstruir y desplegar `web` antes de
probar. `docker start` no despliega código nuevo.

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

## 5. Ejecutar T0

```powershell
dotnet test src\frontend\tests\SG.Web.E2E\SG.Web.E2E.csproj `
  --configuration Debug --no-build `
  --logger "console;verbosity=detailed"
```

Resultado esperado:

- login real y navegación a `/visor`;
- al menos una respuesta HTTP 200 bajo `/api/tiles/`;
- zoom de la instancia MapLibre mayor que 10;
- una captura `visor-minimo-*.png` en `artifacts/e2e`;
- `Total: 1, Failed: 0, Passed: 1`.

Criterio de fallo: variable obligatoria ausente, login rechazado, falta de tile
200 en 30 segundos, mapa no visible, zoom menor o igual a 10, ausencia de la
captura o cualquier prueba roja.

## 6. Prueba de sensibilidad ante regresión

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
