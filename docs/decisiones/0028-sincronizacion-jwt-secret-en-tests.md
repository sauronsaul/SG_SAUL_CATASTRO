# ADR 0028 — Sincronización de JWT Secret en Tests de Integración

**Fecha**: 2026-05-16
**Estado**: Aceptado
**Autores**: Saul Gutierrez + Claude Code

---

## Contexto

Durante T3 del Checkpoint 1.4 (escritura de tests de integración con Testcontainers),
`Me_ConTokenValido_Retorna200ConDatosUsuario` fallaba con HTTP 401 a pesar de que el
login previo retornaba 200 y el access token era no-nulo.

Los otros 6 tests del mismo `AuthE2ETests` pasaban. El único test que requería **validación**
del JWT (no solo generación) era el único en rojo.

---

## Causa raíz

`Program.cs` captura el JWT secret en una variable local durante la fase de composición
del host:

```csharp
// Program.cs — captura temprana
var jwtSecret = builder.Configuration["Jwt:Secret"];   // ← leído aquí
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("JWT_SECRET no configurado...");

builder.Services
    .AddAuthentication(...)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),    // ← usa la variable capturada
            ...
        };
    });
```

`WebApplicationFactory.ConfigureWebHost.ConfigureAppConfiguration` aplica sus fuentes de
configuración **después** de esa captura. El efecto:

| Momento | `builder.Configuration["Jwt:Secret"]` |
|---------|---------------------------------------|
| Línea de captura (fail-fast check) | secret del `.env` local |
| `IConfiguration` en DI (resolución de servicios) | secret del override de la factory |

Resultado:
- `JwtTokenService` genera tokens con el **secret de la factory** (lee `IConfiguration` en runtime).
- `IssuerSigningKey` en bearer validation usa el **secret del `.env`** (capturado en composición).
- Los tokens no se validan → 401.

---

## Decisión

El código productivo **no se modifica**. El acoplamiento temporal en `Program.cs` es
un patrón conocido y aceptable en producción donde el secret no cambia entre composición
y validación.

`SgApiFactory` implementa `PostConfigure<JwtBearerOptions>` para forzar que el signing key
de validación use el mismo secret que `JwtTokenService` recibe en runtime:

```csharp
// SgApiFactory.cs
builder.ConfigureServices(services =>
{
    services.PostConfigure<JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme, opts =>
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(PostgreSqlFixture.JwtSecret));
            opts.TokenValidationParameters.IssuerSigningKey = key;
            opts.TokenValidationParameters.ValidIssuer   = "sg-api-test";
            opts.TokenValidationParameters.ValidAudience = "sg-api-test";
        });
});
```

`PostConfigure` se evalúa después de todos los `Configure` del host, por lo que sobreescribe
los `TokenValidationParameters` configurados en `Program.cs` con los valores de test correctos.

---

## Consecuencias

- Los tests de integración pasan al 100% sin modificar código productivo.
- El fix vive exclusivamente en `SgApiFactory` (capa de test).
- El comportamiento productivo es idéntico al anterior.

---

## Riesgo futuro

Si en el futuro se implementa **rotación de JWT_SECRET sin reinicio de la aplicación**,
el código productivo también necesitaría desacoplar la captura temprana: leer el secret
desde `IConfiguration` en cada validación o usar `IOptionsMonitor<JwtBearerOptions>` para
recargar sin reiniciar. Por ahora este escenario no es un requisito.

---

## Aprendizaje operativo

Los tests de integración con `WebApplicationFactory` revelan **acoplamientos temporales**
(qué se evalúa en qué momento del pipeline de composición del host) que los tests unitarios
con mocks nunca detectan, porque los mocks omiten por completo el pipeline de configuración
de ASP.NET Core.

Esto valida la decisión de incluir tests de integración E2E en el Sprint 1.
