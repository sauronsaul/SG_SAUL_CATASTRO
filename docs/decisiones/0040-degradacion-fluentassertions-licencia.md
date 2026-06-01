# ADR 0040 — Degradación de FluentAssertions de 8.9.0 a 7.2.2

**Estado**: Aceptado
**Fecha**: 2026-05-25
**Sprint**: 3
**Commit que aplica la decisión**: 6a8b536

---

## Contexto

La librería de testing FluentAssertions, hasta la versión 7.x bajo
licencia MIT, pasó en la versión 8.x a licencia comercial gestionada
por Xceed. El uso comercial de la 8.x requiere suscripción de pago;
el uso no-comercial queda bajo términos restrictivos.

El proyecto tenía referenciada la versión 8.9.0 en
`Directory.Packages.props`. La salida de `dotnet test` emitía una
advertencia de licencia en cada ejecución alertando del cambio.

Este es el segundo paquete del proyecto que sufre un cambio de
licencia similar en la misma dirección (MIT → comercial). El
precedente está documentado en ADR 0026 (MediatR).

## Decisión

Degradar FluentAssertions a la versión **7.2.2**, última release bajo
licencia MIT. Cambio aplicado en `Directory.Packages.props`:

    -    <PackageVersion Include="FluentAssertions" Version="8.9.0" />
    +    <PackageVersion Include="FluentAssertions" Version="7.2.2" />

La API de assertions usada por los tests del proyecto es **idéntica**
entre 7.2.2 y 8.9.0 para los métodos en uso (`Should()`, `Be()`,
`BeOfType()`, `Throw<>()`, etc.). Los 156 tests de la suite pasaron
verdes sin cambios de código tras la degradación.

## Consecuencias

**Positivas:**

- El proyecto vuelve a estar 100% bajo licencias compatibles con uso
  comercial municipal sin pagos adicionales.
- La advertencia de licencia desaparece de la salida de `dotnet test`,
  reduciendo el ruido del log.

**Negativas / costos aceptados:**

- Quedamos en una versión que ya no recibe features nuevas. La 7.2.2
  recibirá fixes de seguridad por tiempo limitado pero sin nuevas
  funcionalidades.
- Si en el futuro un test del proyecto requiere una API agregada
  exclusivamente en la 8.x, habrá que evaluar alternativas (escribir
  el assert manualmente, cambiar a otra librería).

## Alternativas descartadas

| Alternativa | Motivo de descarte |
|---|---|
| Pagar suscripción Xceed para FluentAssertions 8.x | Costo recurrente injustificado para una librería de testing cuando hay alternativas gratuitas equivalentes |
| Migrar a Shouldly (otra librería de assertions fluidas, MIT) | Cambio mayor en el código de tests; el costo no se justifica cuando la 7.2.2 cubre todos los usos actuales |
| Migrar a NUnit Asserts nativos | Pérdida significativa de legibilidad en los tests existentes |
| Mantener 8.9.0 con uso "no comercial" | El proyecto se desarrolla para una municipalidad — uso comercial por definición; la 8.x bajo términos restrictivos no aplica |

## Patrón registrado

Esta es la **segunda** dependencia del proyecto que migra de MIT a
licencia comercial restrictiva durante el desarrollo activo:

| Paquete | ADR | Acción |
|---|---|---|
| MediatR | ADR 0026 — MediatR 12.x (MIT) en lugar de MediatR 14.x (licencia comercial) | Degradación a última versión MIT |
| FluentAssertions | ADR 0040 (este) | Degradación a 7.2.2 |

Este patrón es relevante para la planificación: cuando se evalúe
agregar nuevas dependencias al proyecto, considerar como criterio
explícito la **estabilidad histórica del modelo de licencia** del
paquete, no solo su licencia actual.

## Referencias

- ADR 0026 — MediatR 12.x (MIT) en lugar de MediatR 14.x (licencia
  comercial) — precedente directo del mismo patrón
- `Directory.Packages.props` línea 18 — versión activa actual
