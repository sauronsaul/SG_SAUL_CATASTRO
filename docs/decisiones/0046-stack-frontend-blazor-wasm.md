# 0046 — Stack de frontend: Blazor WebAssembly

- **Estado:** Aceptada — decisión ratificada por Saul el 2026-06-13; este archivo es su formalización en `docs/decisiones/`.
- **Fecha decisión:** 2026-06-13 · **Formalizada:** 2026-06-17.
- **Reconstruido** a partir del borrador `BORRADOR_ADR_00XX_stack_frontend.md` (sesión 2026-06-13). El contenido es fiel; se ajustó numeración y formato a la serie.
- **Relación:** condiciona y es condicionado por el ADR 0047 (modelo de despliegue).

## Contexto

El sistema necesita un frontend para los técnicos municipales: gestión de predios, búsqueda por llave, ficha catastral y mapa (Leaflet). Tres hechos enmarcan la decisión:

1. **Desarrollador único de C#.** Saul mantendrá esto solo durante años. Confirmó **no tener experiencia ni preferencia previa en frameworks JavaScript** ("sin preferencias"), dato tratado como sustantivo: no hay capital de equipo en React/Angular/Vue que amortizar.
2. **Backend .NET 10 / Clean Architecture** con DTOs y validadores en C#.
3. **Conectividad municipal variable** (ver ADR 0047): el frontend debe tolerar internet mediocre.

## Decisión

**Blazor WebAssembly**, servido como estáticos por Caddy contra la API existente, con **Leaflet aislado en un único módulo de interop JS**.

Razones dominantes:
- **Un solo lenguaje (C#) y contrato único:** comparte DTOs y validadores con el backend **por referencia de proyecto**, sin codegen y sin npm.
- **Churn de releases LTS** en vez de churn de npm — sostenible para un dev único.
- **Tolerante a internet mediocre** tras la carga inicial cacheada (refuerza el modelo nube-primero del ADR 0047).

## Alternativas consideradas

- **Blazor Server — descartado sin compararlo en detalle.** Requiere circuitos SignalR persistentes, incompatibles con la conectividad municipal variable, y acopla el sistema a "solo servidor local", lo que **invertiría la dependencia** con el ADR de despliegue. Eliminarlo de entrada mantiene libre la decisión de despliegue.
- **React** — objetivamente el mejor framework del mercado **para equipos**; sus ventajas se amortizan en gente que el proyecto no tiene. Añade npm, codegen de tipos y churn de ecosistema sobre un solo par de manos.
- **Angular / Vue** — mismo problema de fondo que React (ecosistema JS, equipo inexistente) sin su ventaja de mercado.
- **Razor + htmx — el plan B honesto.** Server-rendered, simple, sin npm, comparte C#. Cede capacidad para UI rica de mapa/edición. Es la alternativa real si Blazor WASM no rinde, **no** React.

## Consecuencias

- **Las primeras semanas del Sprint 5 son aprendizaje de Blazor, no velocidad plena.** Hay que presupuestarlo explícitamente o el pronóstico (octubre) se resiente.
- **Leaflet vive aislado** en un módulo de interop; todo lo demás es C#. Contiene el único punto de contacto con JS.
- **Caddy** sirve los estáticos del WASM y proxya la API (coherente con el contrato de timeouts del ADR 0043).
- **DTOs compartidos por project reference:** un único contrato backend↔frontend, sin drift de tipos.

## Reversibilidad

Media. Migrar a Razor + htmx conserva backend y DTOs; se reescribe la capa de vista. Mantener Leaflet aislado en interop reduce el costo de un eventual cambio de capa de presentación.
