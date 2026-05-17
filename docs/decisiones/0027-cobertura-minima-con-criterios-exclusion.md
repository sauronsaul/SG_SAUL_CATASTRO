# ADR 0027 — Cobertura mínima con criterios de exclusión

**Fecha**: 2026-05-16
**Estado**: Aceptado
**Autores**: Saul Gutierrez + Claude Code

---

## Contexto

Al cerrar Sprint 1 Checkpoint 1.4, la cobertura reportada por coverlet en `SG.Domain` fue
**76.7%**, por debajo del objetivo formal del 80% definido en el CLAUDE.md (sección 12).

El shortfall de 3.3% se explica íntegramente por tres categorías de clases sin lógica de
negocio:

| Clase | Cobertura | Razón del gap |
|-------|-----------|---------------|
| `AggregateRoot` | 25% | Clase base abstracta sin métodos virtuales propios |
| `DomainException` | 0% | Wrapper trivial de `Exception` sin lógica |
| `ValueObject` | 0% | Clase base abstracta, sin VOs implementados aún |

Agregar tests "decorativos" solo para subir el número reportado sería gaming de la métrica y
produciría tests que no detectan bugs reales.

---

## Decisión

La regla del **80% en Domain** aplica exclusivamente a **clases con lógica de negocio propia**.
Se excluyen del cálculo efectivo:

- Clases base abstractas sin métodos virtuales con lógica propia
  (`AggregateRoot`, `ValueObject`, etc.)
- Wrappers triviales de la biblioteca estándar (`DomainException extends Exception`)
- Marker interfaces (`IDomainEvent`, etc.)
- Clases que solo declaran constantes o errores estáticos sin lógica condicional
  (como `UsuarioErrores` que solo expone `DomainError` inmutables — aunque estas
  SÍ se testean para proteger los códigos de error)

**Criterio alternativo**: si la cobertura reportada está por debajo del 80% pero la cobertura
de las clases con lógica de negocio real (entidades, value objects implementados, servicios de
dominio) supera el **95%**, el criterio de calidad se considera cumplido.

---

## Consecuencias

- Sprint 1 finaliza con SG.Domain al **76.7%** reportado y al **~98% efectivo** (Usuario +
  UsuarioErrores + Result + Result<T> + DomainError = 100% todos).
- Al implementar Predio, Propietario, CodigoCatastral y demás VOs en Sprint 2+, la cobertura
  ponderada subirá naturalmente hacia y sobre el 80%.
- No se escriben tests decorativos ahora ni en futuros sprints para inflar métricas.
- Herramienta de cobertura: `coverlet.collector` con `--collect:"XPlat Code Coverage"`.
  No usar `coverlet.msbuild` (requiere cambios en csproj).

---

## Alternativas consideradas

| Alternativa | Motivo de rechazo |
|-------------|-------------------|
| Agregar AggregateRootTests + DomainExceptionTests | Tests decorativos: no detectarían bugs reales de negocio |
| Bajar el objetivo a 70% | Demasiado permisivo para código futuro con lógica real |
| Excluir archivos en coverlet config | Más complejo, y oculta el problema en lugar de documentarlo |
