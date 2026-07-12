# ADR 0050 — Carga versionada asíncrona con paquete persistido y recuperación por reinicio

**Fecha**: 2026-07-12  
**Estado**: Aceptado

## Contexto

La carga de las siete capas SHP puede exceder el tiempo razonable de una
petición HTTP. La cola del MVP es `Channel<Guid>` en memoria: su contenido se
pierde si el proceso se reinicia.

## Decisión

`POST /api/importaciones/versiones` valida sincrónicamente autorización,
presencia del paquete, tamaño, extensión ZIP y los archivos `.shp`, `.dbf`,
`.shx` y `.prj` requeridos por los perfiles. Un request inválido devuelve 400
sin subir el paquete ni crear una versión.

Para un paquete válido, el endpoint lo sube primero a MinIO, crea y persiste
una `DatasetVersion` en `EnCarga` con la clave MinIO, responde `202 Accepted`
con `Location` y encola su id. El servicio en segundo plano descarga el
paquete desde MinIO y actualiza esa versión con progreso, resultado o error.

Al iniciar, el servicio marca como `Fallida` toda versión `EnCarga`, guarda el
mensaje exacto `carga interrumpida por reinicio` y purga sus capas. Esto es
intencional: al ser la cola efímera, cualquier `EnCarga` existente al arranque
no tiene trabajo pendiente que pueda reanudarse con seguridad.

## Consecuencias

- `Fallida` representa errores de carga, nunca requests malformados.
- El paquete sobrevive al ciclo de vida del request y queda trazado por la
  versión persistida.
- No se reanudan cargas tras reinicio en este MVP; se debe enviar un paquete
  nuevo. Una cola durable es una mejora futura, no parte de esta decisión.
