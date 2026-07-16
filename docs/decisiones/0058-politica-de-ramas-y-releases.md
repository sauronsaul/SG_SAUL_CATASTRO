# ADR 0058 - Política de ramas y releases

**Fecha**: 2026-07-16
**Estado**: Aceptado
**Relación**: formaliza la cosecha operativa de la Fase 2 y reemplaza la
aplicación exclusivamente textual de la política de ramas de `AGENTS.md`.

## Contexto

Los PR #7 y #12 se integraron por error en `main`. En ambos casos, la rama por
defecto del repositorio en GitHub era `main`, por lo que el formulario de un PR
nuevo preseleccionaba esa rama como base. La regla textual «todo PR apunta a
`develop`» ya existía en `AGENTS.md`, pero no evitó que el mismo modo de falla
ocurriera dos veces.

Además, `main` había quedado como una rama huérfana del Sprint 0: no reflejaba
ningún release estable aunque el historial ya contenía los tags `v0.3.0`,
`v0.4.0` y `v0.5.0-fase2-visor`. La separación entre código integrado y código
publicado no tenía representación confiable en las ramas.

## Decisión

- `develop` es la rama de integración continua. Todo el trabajo de fases se
  integra aquí mediante PR cuya base sea explícitamente `develop`.
- `main` es la rama de releases y refleja siempre el último release estable.
- La rama por defecto del repositorio en GitHub se cambió de `main` a
  `develop`. Este cambio corrige la causa raíz: el formulario de PR ahora
  preselecciona la base correcta. Aun así, antes de crear y antes de mergear un
  PR se verifica visualmente que la base sea `develop`.
- Al cerrar una fase con la suite completa en verde, el flujo de release para
  el mantenedor único es:

  ```powershell
  git checkout main
  git merge --ff-only develop
  git tag vX.Y.Z-<nombre-fase>
  git push origin main --tags
  ```

- No se usa un PR `develop` → `main` en la práctica actual. El release es una
  operación lineal mediante `--ff-only`, sin un selector de base que pueda
  leerse mal, mientras el repositorio tenga un solo desarrollador.
- Los tags de release se colocan sobre `main`. Por la condición `--ff-only`,
  `main` y `develop` apuntan al mismo commit al momento de publicar.
- `main` se alineó retroactivamente mediante fast-forward con
  `v0.5.0-fase2-visor`, commit `b2772d2`, para dejar de ser una rama huérfana
  del Sprint 0.
- Los tags anteriores `v0.3.0` y `v0.4.0` permanecen como marcadores
  históricos sobre `develop`. No se mueven ni se reescribe el historial.

## Evolución futura

Cuando ingresen colaboradores, el flujo de release debe migrar a PR
`develop` → `main` con reglas de protección que exijan explícitamente
`base = main` y checks de CI en verde antes del merge. Esa práctica no se
implementa hoy: con un solo desarrollador agregaría ceremonia sin seguridad
adicional respecto de un fast-forward verificado.

## Consecuencias

- `main` recupera un significado operativo verificable: último release
  estable. La protección de esa rama queda justificada por su función.
- `develop` representa código integrado que puede estar entre releases;
  `main`, código publicado.
- La convención coincide con un flujo estándar y queda documentada en el
  repositorio, sin depender de reglas conocidas sólo por el mantenedor.
- Cambiar la rama por defecto reduce la probabilidad de PR accidentales a
  `main`, pero no elimina la verificación visual obligatoria de la base.
- `git merge --ff-only develop` impide crear un release si `main` divergió; en
  ese caso se detiene la operación y se diagnostica la divergencia en vez de
  producir un merge implícito.
