# ADR 0029 — Migraciones automáticas al arranque de la API

**Fecha**: 2026-05-16
**Estado**: Aceptado
**Autores**: Saul Gutierrez + Claude Code

---

## Contexto

El contenedor Docker de la API usa la imagen `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`
(runtime only, sin SDK). Esto significa que `dotnet ef database update` no está disponible
en runtime dentro del contenedor.

En modo local el operador no tiene conocimiento del ciclo de vida de EF Core. El sistema
debe autoaplicar migraciones pendientes en el primer arranque y en cada actualización,
sin intervención manual.

---

## Decisión

`Program.cs` llama `db.Database.MigrateAsync()` durante el startup de la API, antes del
seeder y antes de `app.Run()`.

**Orden obligatorio e invariable:**

```
1. MigrateAsync()   — aplica todas las migraciones pendientes
2. SeedAsync()      — requiere que las tablas ya existan (roles, usuarios)
3. app.Run()        — la API empieza a aceptar tráfico
```

Si la base de datos ya está al día, `MigrateAsync` es un no-op de milisegundos.
Si hay migraciones pendientes, las aplica antes de que ninguna request entre.

---

## Consecuencias

- El operador nunca necesita ejecutar comandos de migración manualmente.
- Los healthchecks del contenedor esperan el `start_period` (60 s) configurado en
  `docker-compose.yml`, suficiente para que migraciones grandes completen.
- En `dotnet run` (desarrollo local sin Docker) el comportamiento es idéntico:
  `MigrateAsync` es no-op si la base ya está al día.

---

## Riesgo futuro

En modo servidor con **múltiples instancias** de la API corriendo simultáneamente,
`MigrateAsync` puede ejecutarse concurrentemente en cada instancia al arrancar.
EF Core aplica un lock de base de datos, por lo que las migraciones no se duplican,
pero el riesgo es un timeout si la migración es larga y las instancias compiten.

**Mitigación futura aceptable:** distributed lock explícito (ej. tabla `migration_lock`)
o migration job separado (Kubernetes init container). Por ahora el modo local es
single-instance y este riesgo no aplica.

---

## Alternativas descartadas

| Alternativa | Motivo de descarte |
|---|---|
| `dotnet ef database update` en Dockerfile (CMD o ENTRYPOINT) | Requiere SDK en imagen de runtime; aumenta ~700 MB la imagen final |
| Script SQL de init de PostgreSQL (`docker-entrypoint-initdb.d`) | Solo corre en el primer `initdb`; no aplica migraciones posteriores |
| Migration job separado como init container Docker | Correcto en Kubernetes, excesivamente complejo para modo local single-instance |
| Manual por el operador | Inaceptable para municipios sin conocimiento técnico de EF Core |
