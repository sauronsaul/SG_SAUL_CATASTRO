# ADR 0026 — MediatR 12.x (MIT) en lugar de MediatR 14.x (licencia comercial)

**Estado**: Aceptado  
**Fecha**: 2026-05-15  
**Sprint**: 1 — Checkpoint 1.3  
**Autor**: Saul Gutierrez

---

## Contexto

Durante las pruebas de Checkpoint 1.3 se detectó el siguiente mensaje en los logs de arranque de la API:

```
MediatR 14.x requires a commercial license for production use.
Please visit https://luckypennysoftware.com for more information.
```

**Causa**: MediatR 14.x (publicado por Jimmy Bogard bajo Lucky Penny Software) cambió de licencia MIT a licencia comercial de pago. Esto aplica a uso en producción. El warning aparece en tiempo de ejecución como alerta explícita del paquete.

**Versión instalada al detectar el problema**: `MediatR 14.1.0`

---

## Decisión

**Revertir MediatR a la versión 12.5.0**, que es la última versión publicada bajo licencia MIT sin restricción comercial.

Cambio aplicado en `Directory.Packages.props`:

```xml
<!-- Antes -->
<PackageVersion Include="MediatR" Version="14.1.0" />

<!-- Después -->
<PackageVersion Include="MediatR" Version="12.5.0" />
```

No se requirió `MediatR.Contracts` como paquete separado: en 12.x las interfaces se distribuyen dentro del paquete principal.

---

## Análisis de compatibilidad

Se auditaron todos los usos de MediatR en el codebase:

| Tipo / API | Usado en | Disponible en 12.5.0 | Disponible en 14.x |
|---|---|---|---|
| `IRequest<TResponse>` | Commands y Queries | ✓ | ✓ |
| `IRequestHandler<TRequest, TResponse>` | Handlers | ✓ | ✓ |
| `ISender` | AuthController | ✓ | ✓ |
| `AddMediatR(cfg => cfg.RegisterServicesFromAssembly())` | DependencyInjection.cs | ✓ | ✓ |

**No se requirió ningún cambio de código.** La API compila con 0 errores y 0 warnings de compilación. Los 11 tests siguen en verde.

---

## Justificación

1. **SG_SAUL_CATASTRO es un sistema institucional municipal** sin presupuesto para licencias de software de terceros. El modelo de costo cero es un requisito no negociable.

2. **MediatR 12.5.0 cubre el 100% de los casos de uso actuales y del roadmap MVP** (CQRS básico: commands, queries, handlers, registro DI). Las funcionalidades agregadas en 13.x y 14.x (streaming mejorado, algunas optimizaciones internas) no están en el plan.

3. **La reversión no genera deuda técnica funcional.** MediatR 12.5.0 es compatible con .NET 10 (resuelve contra el TFM `net6.0` del paquete, que es forward-compatible con runtimes posteriores).

4. **Alternativa futura**: Si en Sprints posteriores se requieren features exclusivos de 14.x, se evaluará Mediator de Zapto9Studios o un bus de mensajes propio, ambos MIT.

---

## Consecuencias

### Positivas
- Warning de licencia eliminado de los logs de arranque.
- Cumplimiento de la política de uso exclusivo de software libre/MIT.
- Sin cambios de código ni riesgos de regresión.

### Negativas / Restricciones
- No se puede usar funcionalidad introducida en MediatR 13.x o 14.x.
- Si upstream publica una corrección de seguridad crítica solo en 14.x, se requerirá evaluar alternativas.

---

## Referencias

- [Directory.Packages.props](../../src/backend/Directory.Packages.props)
- [SG.Application/DependencyInjection.cs](../../src/backend/SG.Application/DependencyInjection.cs)
- MediatR changelog: https://github.com/jbogard/MediatR/releases
- Lucky Penny Software licensing: https://luckypennysoftware.com
