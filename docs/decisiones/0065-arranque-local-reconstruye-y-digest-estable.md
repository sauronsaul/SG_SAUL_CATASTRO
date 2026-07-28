# ADR 0065 — El arranque local reconstruye imágenes y estabiliza su digest

**Fecha**: 2026-07-28
**Estado**: Aceptado
**Autores**: Saul Gutierrez + equipo del proyecto
**Relacionado**: ADR 0048 (modelo híbrido nativo/contenedor)

---

## Contexto

El 28 de julio de 2026 se detectó que los contenedores `sg_api` y `sg_web`
ejecutaban código sensiblemente anterior al de `develop`.

`sg_api` corría una imagen (`sha256:94ffe232…`) que **ya no existía en el almacén
local de Docker**: el tag `sg-catastro-api:latest` apuntaba a otra imagen
distinta, y el contenedor seguía vivo sobre una capa huérfana. No era posible
inspeccionar ni reconstruir esa imagen, de modo que no había forma de establecer
qué código contenía. Síntoma observable: el endpoint
`POST /api/importaciones/versiones/{id}/descartar` devolvía 404 pese a existir en
`ImportacionesController.cs:117` desde el commit `36fe8e4`.

`sg_web` presentaba deriva equivalente por otra vía: su imagen se construyó ocho
minutos antes del commit `407a70b`, que habilita la configuración multimunicipio
del visor.

La consecuencia grave no es el 404 concreto, sino que **durante días toda
evidencia obtenida por HTTP fue inatribuible a una versión conocida del código**.
En un proyecto cuyo protocolo operativo exige evidencia cruda antes de cerrar
cualquier paso, un binario de procedencia desconocida invalida silenciosamente
esa evidencia.

Causa: `scripts/start-local.sh` invocaba
`docker compose up -d --remove-orphans`, sin `--build`. Un contenedor existente
sobrevive intacto aunque la imagen se reconstruya por otra vía, y nada en el
arranque señala la discrepancia.

## Decisión

`scripts/start-local.sh` reconstruye las imágenes en cada arranque y fija el
digest resultante:

1. Se añade `--build` a la invocación de `docker compose up`.
2. Se exporta `BUILDX_NO_DEFAULT_ATTESTATIONS=1` antes de esa invocación.

Ambos cambios son necesarios. El primero garantiza correctitud; el segundo evita
el coste que el primero introduce por sí solo.

## Justificación de la segunda medida

`--build` aislado tiene un efecto que no se anticipó y que se descubrió al
probarlo: **recreaba los contenedores en cada arranque incluso sin cambios de
código**. Medido en tres ejecuciones consecutivas sobre un árbol sin
modificaciones, cada una producía una imagen con digest distinto y compose
recreaba los contenedores, con un coste aproximado de 25 a 30 segundos por
arranque.

La causa es que BuildKit adjunta por defecto atestaciones de procedencia con
marca de tiempo. Aunque todas las capas se resuelvan `CACHED`, el *attestation
manifest* y por tanto el *manifest list* final cambian de digest en cada build.
Compose compara digests, observa una imagen distinta y recrea el servicio.

Verificación con la variable activa, tres arranques consecutivos sin cambios:

    id 1 = id 2 = id 3 = sha256:0447714d825a55293e6ae7285e79d5397fdf8e7da4356f3a6772a5c61c81d552
    contenedor 1 = 2 = 3 = 2026-07-28T20:56:40.986355243Z

Digest de imagen y fecha de contenedor invariables. Con un cambio real en el
código, en cambio, la imagen y el contenedor sí se renuevan; verificado
modificando `TilesController.cs` y observando imagen y contenedor posteriores a
la ejecución previa.

Las atestaciones de procedencia no aportan valor en el entorno de desarrollo
local. Esta decisión no afecta a `docker-compose.prod.yml` ni a ninguna
construcción de despliegue.

## Alternativa descartada

Se evaluó añadir al script una detección de deriva —comparar la fecha de creación
de cada imagen contra el último commit de `src/backend` y `src/frontend`, y
advertir— en lugar de reconstruir. Se descarta: diagnostica el síntoma en vez de
eliminar la causa, y añade superficie de fallo a un script de arranque, cuya
fiabilidad importa más que su capacidad informativa.

## Consecuencias

- El arranque local queda vinculado al estado del árbol de trabajo. Los
  contenedores dejan de poder quedar por detrás del código sin que nadie lo
  advierta.
- **Un build roto impide arrancar.** `start-local.sh` tiene `set -euo pipefail`,
  de modo que un fallo de compilación aborta el arranque completo. Antes, con el
  código roto se levantaba la última imagen buena. Se considera preferible fallar
  de forma visible a servir código fantasma, pero es un cambio real de
  comportamiento: para levantar solo la infraestructura durante una refactori-
  zación que no compila, debe invocarse compose directamente sobre los servicios
  necesarios.
- Al recrearse la API en un arranque con cambios, se ejecuta
  `MarcarHuerfanasAlArrancarAsync`, que marca como `Fallida` toda `DatasetVersion`
  en estado `EnCarga` y purga sus filas. No debe arrancarse el entorno con una
  importación en vuelo.
- Se pierden las atestaciones de procedencia en las imágenes locales. Sin efecto
  fuera del entorno de desarrollo.
- `scripts/stop-local.sh` no requiere cambios.
