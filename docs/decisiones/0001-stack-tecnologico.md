# ADR 0001 — Stack Tecnológico

**Fecha**: 2026-05-07
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Se requiere definir el stack tecnológico completo del sistema SG_SAUL_CATASTRO antes de iniciar el desarrollo. El sistema debe:

- Ejecutarse en municipios pequeños con una sola PC (modo local con Docker Desktop).
- Manejar datos geoespaciales (predios, geometrías, zonificación).
- Ser mantenible por un único desarrollador a largo plazo.
- Cumplir normativa boliviana de catastro urbano.
- Proveer auditoría completa de operaciones.
- Soportar crecimiento hacia modo servidor multiusuario sin reescribir el núcleo.

## Decisión

Se adopta el siguiente stack oficial (no modificar sin nueva ADR):

### Backend
- **.NET 10 LTS** + **C# 13** + **ASP.NET Core 10** — soporte hasta noviembre 2028; ecosistema maduro para sistemas institucionales.
- **Entity Framework Core 10** — ORM con soporte PostGIS vía NetTopologySuite.
- **MediatR** — implementación de CQRS sin overhead de frameworks de eventos complejos.
- **FluentValidation** — validación declarativa desacoplada de los handlers.
- **Mapster** — mapeo de objetos más liviano que AutoMapper.
- **QuestPDF** — generación de PDFs sin dependencias nativas.
- **ASP.NET Core Identity + JWT + BCrypt.Net-Next** — autenticación estándar con hash seguro.
- **Serilog** — logs estructurados con sinks configurables.

### Frontend
- **Vite 5 + React 18 + TypeScript 5** — build tool moderno, ecosistema React maduro.
- **Ant Design 5** — librería de componentes enterprise con soporte RTL y localización, adecuada para formularios institucionales complejos.
- **TanStack Query** — gestión de estado servidor con caché y revalidación.
- **Zustand** — estado cliente minimal sin boilerplate.
- **MapLibre GL JS** — visor de mapas open-source sin dependencia de API keys de Mapbox.

### Datos e infraestructura
- **PostgreSQL 16 + PostGIS 3.4** — base de datos relacional con soporte geoespacial nativo; única opción práctica para catastro con geometrías.
- **MinIO** — almacenamiento de documentos S3-compatible, self-hosted, sin costos de nube.
- **pg_tileserv + tileserver-gl** — tiles vectoriales y raster desde la propia BD, sin dependencia de servicios externos.
- **Caddy** — reverse proxy con TLS automático; configuración simple para modo local y servidor.
- **Docker + Docker Compose** — contenerización para reproducibilidad entre entornos.

## Consecuencias

**Positivas**:
- Stack coherente y alineado con ecosistemas maduros.
- Soporte LTS garantizado hasta 2028 para .NET 10.
- Capacidad GIS sin capas adicionales (PostGIS nativo).
- Despliegue reproducible en cualquier PC con Docker Desktop.
- Sin costos de licencia ni dependencias de servicios cloud.

**Negativas / compromisos**:
- Node.js v25 en entorno actual (pendiente migrar a 22 LTS — no bloqueante para MVP).
- Curva de aprendizaje de Clean Architecture para futuros colaboradores.
- El sistema completo requiere Docker Desktop funcionando; no hay modo "sin Docker".

## Alternativas descartadas

| Alternativa | Razón de descarte |
|---|---|
| Django/Python backend | Ecosistema GIS potente pero integración con .NET innecesaria; equipo ya conoce C#. |
| Next.js en lugar de Vite+React | SSR no agrega valor para sistema de backoffice municipal sin SEO. |
| SQLite | Sin soporte PostGIS; inviable para geometrías. |
| MongoDB | Sin transacciones ACID ni soporte geoespacial comparable a PostGIS. |
| AutoMapper | Mapster es más liviano y tiene mejor rendimiento para volúmenes institucionales. |
