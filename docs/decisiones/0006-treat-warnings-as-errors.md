# ADR 0006 — TreatWarningsAsErrors y política de supresión

**Fecha**: 2026-05-11
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Un proyecto institucional de largo plazo necesita que la calidad del código
se mantenga sin depender de la disciplina manual de cada sesión. Las
advertencias del compilador y los analizadores son síntomas de problemas
reales (código muerto, APIs inseguras, violaciones de estilo) que si no se
exigen, se acumulan silenciosamente hasta convertirse en deuda técnica.

## Decisión

En `src/backend/Directory.Build.props`:

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<WarningsAsErrors />
<AnalysisLevel>latest-recommended</AnalysisLevel>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);CS1591</NoWarn>
```

Toda advertencia del compilador o de los analizadores Roslyn rompe el build.
No se puede mergear código que no compile limpio.

## Supresiones globales activas

| Código | Descripción | Justificación |
|---|---|---|
| `CS1591` | Falta documentación XML en miembro público | `GenerateDocumentationFile=true` lo activa en todo el código público. Se exigirá documentación selectivamente a medida que el dominio madure, no de forma masiva desde el inicio. |

## Supresiones específicas para proyectos de prueba

En `src/backend/tests/Directory.Build.props`:

| Código | Descripción | Justificación |
|---|---|---|
| `CA1707` | Identificadores no deben contener guiones bajos | Los métodos de test usan convención `Metodo_Deberia_Comportamiento` con guiones bajos. |
| `CA1822` | Marcar miembros como estáticos | Los métodos `[Fact]` y `[Theory]` de xUnit deben ser de instancia. El analizador no conoce el atributo xUnit y los marcaría incorrectamente. |

Además, los proyectos de prueba tienen `GenerateDocumentationFile=false`
porque los tests no necesitan documentación XML pública.

## Procedimiento ante un warning nuevo

1. Identificar la causa raíz — es casi siempre un problema real.
2. Corregir el código si es posible.
3. Si la supresión es legítima (API externa, patrón conocido), agregar a
   `<NoWarn>` en `Directory.Build.props` con comentario justificando.
4. Nunca usar `#pragma warning disable` sin documentar el motivo en el mismo
   bloque de código.
5. Nunca deshabilitar `TreatWarningsAsErrors`.

## Consecuencias

**Positivas**:
- El build verde garantiza zero warnings en toda la solución.
- Los analizadores `latest-recommended` detectan problemas de rendimiento,
  corrección y estilo antes de que lleguen a producción.
- CI rechaza automáticamente PRs con código degradado.

**Negativas / compromisos**:
- Paquetes de terceros que generen warnings en sus APIs públicas pueden
  requerir entradas adicionales en `<NoWarn>`.
- Requiere atención consciente al agregar nuevas dependencias.
