# 0047 — Modelo de despliegue: nube-primero

- **Estado:** Aceptada — decisión ratificada por Saul el 2026-06-13; este archivo es su formalización en `docs/decisiones/`.
- **Fecha decisión:** 2026-06-13 · **Formalizada:** 2026-06-17.
- **Reconstruido** a partir del borrador `BORRADOR_ADR_00XX_modelo_despliegue.md` (sesión 2026-06-13); recuperado casi literal.
- **Relación:** se apoya en el ADR 0046 (Blazor WASM, tolerante a internet mediocre tras la carga cacheada).

## Contexto

El sistema se desplegará para N municipios (GAMs). El proveedor es un **desarrollador único**. La conectividad de las alcaldías es **variable**. Existe una posible **objeción de soberanía de datos** (AGETIC / localización de datos estatales). Hay que fijar el modelo de despliegue por defecto antes de Sprint 5 y antes del primer contrato.

## Opciones evaluadas

**A. On-premise (servidor local en cada alcaldía).**
Cada instalación es un **pasivo permanente**: hardware que alguien debe comprar, mantener y respaldar; soporte remoto dependiente de que el internet municipal funcione justo cuando algo falló; N municipios = N entornos divergentes (versiones, parches, relojes, discos llenos); el responsable real de los backups termina siendo nadie. Para un dev único, **escala linealmente en contra**.

**B. Nube / VPS administrado por el proveedor — uno por municipio.**
Un solo lugar donde actualizar (compose pull por instancia, automatizable), monitorear y respaldar; **backups off-site centralizados** (pg_dump + objetos MinIO hacia storage externo) bajo control de quien sí sabe hacerlos; ningún viaje; **aislamiento fuerte** entre municipios (una instancia = un stack = cero multi-tenant sobre el esquema actual); costo predecible (~10–40 USD/mes por VPS chico, trasladable a la licencia). En contra: exige internet razonable (criterio de elegibilidad por GAM, no supuesto universal); posible objeción de soberanía (resoluble con datacenter en Bolivia si hay oferta aceptable — verificar); dependencia del proveedor (necesita salida contractual: export de datos + imagen del stack).

**C. Híbrido sin política ("el que quiera local, local").**
Es la respuesta comercial disfrazada de arquitectura. Sin condiciones explícitas degenera en la opción A para los GAM más complicados, que son justo los que menos personal técnico tienen. **Descartado como política por defecto; rescatado como excepción condicionada.**

**Variante descartada — multi-tenant** (una instancia compartida, esquemas por municipio). Reduciría costo de VPS pero introduce análisis de aislamiento sobre un esquema diseñado single-tenant (`identidad.*`, auditoría, buckets MinIO) y un radio de explosión compartido. Para el volumen esperado 2026–2027 el ahorro no paga el riesgo. Reevaluable si el producto supera ~15–20 municipios.

## Decisión

**Nube-primero (opción B) como modelo por defecto y único soportado en v1.0.** Una instancia aislada del stack por municipio, administrada por el proveedor, con backups off-site automatizados y verificados.

**On-premise solo como excepción contractual, sujeta a TODAS estas condiciones:**
1. El GAM provee el hardware con specs mínimas definidas.
2. Designa un responsable técnico con nombre y cargo.
3. Acepta por escrito el reparto de responsabilidad sobre backups.
4. Paga la prima de soporte correspondiente.

**Sin las cuatro, la respuesta es no.**

**Piloto Caranavi: VPS.** Es además el entorno donde el dev único puede observar el sistema en producción sin viajar.

## Consecuencias

- La pregunta **"¿cómo es el internet en su alcaldía?"** entra a la ficha de interlocutor por municipio.
- La **verificación normativa AGETIC** (localización de datos estatales) es **tarea previa a firmar el primer contrato**; puede cambiar el proveedor de VPS, no el modelo.
- El **runbook de instancia** (provisión, actualización, backup, **restore PROBADO**) se vuelve entregable de primera clase antes del piloto: con nube-primero, ese runbook **es** el producto operativo.
- Refuerza la elección de Blazor WASM (ADR 0046).

## Reversibilidad

Alta en dirección B→A (el stack ya corre en compose en cualquier fierro; migrar un municipio a local es mover volúmenes). Baja en dirección A→B si se permitió divergencia de entornos — razón adicional para no abrir la puerta on-premise sin las cuatro condiciones.
