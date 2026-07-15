const mapas = new Map();
const prefijoLog = "[SG.Web mapa]";
const capaResaltadoRelleno = "parcelas-seleccion-relleno";
const capaResaltadoLinea = "parcelas-seleccion-linea";

export function crearMapa(contenedorId, limites, camara, token, capas, referenciaDotNet) {
    if (!window.maplibregl) {
        throw new Error("MapLibre GL JS 5.24.0 no esta disponible.");
    }

    const cantidadCapas = Array.isArray(capas) ? capas.length : -1;
    console.info(`${prefijoLog} creación`, { contenedorId, cantidadCapas, bbox: limites });
    if (!Array.isArray(capas) || capas.length !== 7) {
        throw new Error(`${prefijoLog} se esperaban 7 capas y llegaron ${cantidadCapas}.`);
    }
    validarLimites(limites);

    const contenedor = document.getElementById(contenedorId);
    if (!contenedor) {
        throw new Error(`${prefijoLog} no existe el contenedor ${contenedorId}.`);
    }

    destruirMapa(contenedorId);

    const opciones = {
        container: contenedorId,
        style: {
            version: 8,
            sources: {},
            layers: [{ id: "fondo", type: "background", paint: { "background-color": "#eef2f1" } }]
        },
        attributionControl: false,
        maxZoom: 22,
        transformRequest: (url, tipoRecurso) => transformarSolicitud(url, tipoRecurso, token)
    };

    const mapa = new window.maplibregl.Map(opciones);
    mapa.addControl(new window.maplibregl.NavigationControl({ showCompass: false }), "top-right");

    const estado = {
        mapa,
        notificado401: false,
        capasInicializadas: false,
        estiloListo: false,
        dimensiones: null,
        cancelarEsperaDimensiones: null,
        referenciaDotNet
    };
    mapas.set(contenedorId, estado);

    mapa.on("load", () => {
        console.info(`${prefijoLog} evento load`);
    });

    const agregarCapas = () => {
        if (estado.capasInicializadas || !estado.estiloListo || !estado.dimensiones) return;

        estado.cancelarEsperaDimensiones?.();
        estado.cancelarEsperaDimensiones = null;
        aplicarEncuadre(mapa, limites, camara, estado.dimensiones);
        estado.capasInicializadas = true;
        console.info(`${prefijoLog} inicialización de capas`, { cantidadCapas: capas.length });

        for (const capa of capas) {
            const plantillaTile = crearPlantillaTile(capa.nombre);
            console.info(`${prefijoLog} addSource`, {
                capa: capa.nombre,
                minZoom: capa.minZoom,
                maxZoom: 22,
                urlTemplate: plantillaTile
            });
            mapa.addSource(capa.nombre, {
                type: "vector",
                tiles: [plantillaTile],
                minzoom: capa.minZoom,
                maxzoom: 22
            });
        }

        for (const capa of capas.filter(x => x.tieneRelleno)) {
            mapa.addLayer(crearRelleno(capa));
        }

        for (const capa of capas.filter(x => x.tieneLinea)) {
            mapa.addLayer(crearLinea(capa));
        }

        for (const capa of capas.filter(x => x.campoEtiqueta)) {
            mapa.addLayer(crearEtiqueta(capa));
        }

        mapa.addLayer(crearResaltadoRelleno());
        mapa.addLayer(crearResaltadoLinea());

        console.info(`${prefijoLog} capas listas`, {
            fuentes: capas.length,
            capasDibujadas: capas.filter(x => x.tieneRelleno).length
                + capas.filter(x => x.tieneLinea).length
                + capas.filter(x => x.campoEtiqueta).length
                + 2
        });

        mapa.on("click", evento => seleccionarParcela(mapa, evento, referenciaDotNet));
    };

    if (mapa.isStyleLoaded()) {
        console.info(`${prefijoLog} estilo ya cargado`);
        estado.estiloListo = true;
        agregarCapas();
    } else {
        console.info(`${prefijoLog} esperando evento style.load`);
        mapa.once("style.load", () => {
            console.info(`${prefijoLog} evento style.load`);
            estado.estiloListo = true;
            agregarCapas();
        });
    }

    estado.cancelarEsperaDimensiones = esperarDimensiones(contenedor, (dimensiones) => {
        estado.dimensiones = dimensiones;
        agregarCapas();
    });

    mapa.on("error", (evento) => {
        const status = evento?.error?.status;
        if (status === 401 && !estado.notificado401) {
            estado.notificado401 = true;
            void referenciaDotNet.invokeMethodAsync("NotificarNoAutorizadoAsync");
            return;
        }

        console.error(`${prefijoLog} error MapLibre`, {
            status: status ?? null,
            url: evento?.error?.url ?? null,
            message: evento?.error?.message ?? "Error sin detalle"
        });
    });
}

function validarLimites(limites) {
    if (!Array.isArray(limites)
        || limites.length !== 4
        || limites.some(limite => !Number.isFinite(limite))
        || limites[0] >= limites[2]
        || limites[1] >= limites[3]) {
        throw new Error(`${prefijoLog} bbox inválido; se esperaba [oeste, sur, este, norte].`);
    }
}

function esperarDimensiones(contenedor, alEstarListo) {
    let terminado = false;
    let frameId = null;
    let observador = null;

    const comprobar = () => {
        if (terminado) return true;

        const w = contenedor.clientWidth;
        const h = contenedor.clientHeight;
        if (w <= 0 || h <= 0) return false;

        terminado = true;
        observador?.disconnect();
        if (frameId !== null) {
            window.cancelAnimationFrame(frameId);
            frameId = null;
        }

        alEstarListo({ w, h });
        return true;
    };

    if (typeof window.ResizeObserver === "function") {
        observador = new window.ResizeObserver(comprobar);
        observador.observe(contenedor);
    }

    const programarFrame = () => {
        frameId = window.requestAnimationFrame(() => {
            frameId = null;
            if (!comprobar() && !observador) {
                programarFrame();
            }
        });
    };
    programarFrame();

    return () => {
        terminado = true;
        observador?.disconnect();
        if (frameId !== null) {
            window.cancelAnimationFrame(frameId);
        }
    };
}

function aplicarEncuadre(mapa, limites, camara, dimensiones) {
    mapa.resize();

    if (camara) {
        mapa.jumpTo({
            center: [camara.longitud, camara.latitud],
            zoom: camara.zoom
        });
    } else {
        mapa.fitBounds(
            [[limites[0], limites[1]], [limites[2], limites[3]]],
            { padding: 40, maxZoom: 14, duration: 0 });
    }

    const centro = mapa.getCenter();
    const detalle = {
        center: [centro.lng, centro.lat],
        zoom: mapa.getZoom(),
        w: dimensiones.w,
        h: dimensiones.h
    };
    console.info(`${prefijoLog} encuadre aplicado`, detalle);

    if (!camara && detalle.zoom <= 1) {
        console.warn(`${prefijoLog} encuadre municipal inválido: zoom <= 1`, {
            ...detalle,
            bbox: limites
        });
        throw new Error(`${prefijoLog} no se pudo encuadrar el bbox municipal.`);
    }
}

export function cambiarVisibilidad(contenedorId, nombreCapa, visible) {
    const mapa = mapas.get(contenedorId)?.mapa;
    if (!mapa) return;

    for (const sufijo of ["relleno", "linea", "etiqueta"]) {
        const id = `${nombreCapa}-${sufijo}`;
        if (mapa.getLayer(id)) {
            mapa.setLayoutProperty(id, "visibility", visible ? "visible" : "none");
        }
    }
}

export function obtenerCamara(contenedorId) {
    const mapa = mapas.get(contenedorId)?.mapa;
    if (!mapa) return null;
    const centro = mapa.getCenter();
    return { longitud: centro.lng, latitud: centro.lat, zoom: mapa.getZoom() };
}

export function enfocarPredio(contenedorId, limites) {
    const mapa = mapas.get(contenedorId)?.mapa;
    if (!mapa) return;

    const bbox = [limites.oeste, limites.sur, limites.este, limites.norte];
    validarLimites(bbox);
    mapa.fitBounds(
        [[bbox[0], bbox[1]], [bbox[2], bbox[3]]],
        { padding: 64, maxZoom: 19, duration: 500 });
    console.info(`${prefijoLog} predio enfocado`, { bbox });
}

export function resaltarPredio(contenedorId, distrito, manzana, predio) {
    const mapa = mapas.get(contenedorId)?.mapa;
    if (!mapa) return;

    const triplete = [distrito, manzana, predio];
    if (!triplete.every(Number.isInteger) || triplete.some(valor => valor < 1)) {
        throw new Error(`${prefijoLog} triplete inválido para resaltado.`);
    }

    const filtro = crearFiltroTriplete(distrito, manzana, predio);
    mapa.setFilter(capaResaltadoRelleno, filtro);
    mapa.setFilter(capaResaltadoLinea, filtro);
    console.info(`${prefijoLog} predio resaltado`, { distrito, manzana, predio });
}

export function limpiarResaltado(contenedorId) {
    const mapa = mapas.get(contenedorId)?.mapa;
    if (!mapa) return;

    const filtro = crearFiltroSinSeleccion();
    mapa.setFilter(capaResaltadoRelleno, filtro);
    mapa.setFilter(capaResaltadoLinea, filtro);
    console.info(`${prefijoLog} resaltado limpiado`);
}

export function obtenerResaltado(contenedorId) {
    const mapa = mapas.get(contenedorId)?.mapa;
    if (!mapa) return null;
    return {
        relleno: mapa.getFilter(capaResaltadoRelleno),
        linea: mapa.getFilter(capaResaltadoLinea)
    };
}

export function destruirMapa(contenedorId) {
    const estado = mapas.get(contenedorId);
    if (!estado) return;
    estado.cancelarEsperaDimensiones?.();
    estado.mapa.remove();
    mapas.delete(contenedorId);
}

function crearPlantillaTile(nombreCapa) {
    const baseTiles = new URL("/api/tiles/", window.location.origin).href;
    return `${baseTiles}${encodeURIComponent(nombreCapa)}/{z}/{x}/{y}.mvt`;
}

function transformarSolicitud(url, tipoRecurso, token) {
    const destino = new URL(url, window.location.origin);
    const esTileMismoOrigen = destino.origin === window.location.origin
        && destino.pathname.startsWith("/api/tiles/");
    console.info(`${prefijoLog} transformRequest`, {
        resourceType: tipoRecurso ?? null,
        url: destino.href,
        autenticada: esTileMismoOrigen
    });

    if (esTileMismoOrigen) {
        return {
            url: destino.href,
            headers: { Authorization: `Bearer ${token}` }
        };
    }
    return { url };
}

function seleccionarParcela(mapa, evento, referenciaDotNet) {
    const features = mapa.queryRenderedFeatures(evento.point, {
        layers: ["parcelas-relleno"]
    });
    const feature = features.find(item => item?.properties);
    if (!feature) return;

    const distrito = Number(feature.properties.cod_uv);
    const manzana = Number(feature.properties.cod_man);
    const predio = Number(feature.properties.cod_pred);
    if (![distrito, manzana, predio].every(Number.isInteger)
        || distrito < 1 || manzana < 1 || predio < 1) {
        console.warn(`${prefijoLog} parcela sin triplete válido`, { featureId: feature.id ?? null });
        return;
    }

    console.info(`${prefijoLog} parcela seleccionada`, {
        featureId: feature.id ?? null,
        distrito,
        manzana,
        predio
    });
    void referenciaDotNet.invokeMethodAsync(
        "SeleccionarPredioAsync",
        distrito,
        manzana,
        predio);
}

function crearFiltroTriplete(distrito, manzana, predio) {
    return [
        "all",
        ["==", ["get", "cod_uv"], distrito],
        ["==", ["get", "cod_man"], manzana],
        ["==", ["get", "cod_pred"], predio]
    ];
}

function crearFiltroSinSeleccion() {
    return ["==", ["get", "cod_uv"], -1];
}

function crearResaltadoRelleno() {
    return {
        id: capaResaltadoRelleno,
        type: "fill",
        source: "parcelas",
        "source-layer": "parcelas",
        minzoom: 15,
        filter: crearFiltroSinSeleccion(),
        paint: {
            "fill-color": "#FACC15",
            "fill-opacity": 0.38
        }
    };
}

function crearResaltadoLinea() {
    return {
        id: capaResaltadoLinea,
        type: "line",
        source: "parcelas",
        "source-layer": "parcelas",
        minzoom: 15,
        filter: crearFiltroSinSeleccion(),
        paint: {
            "line-color": "#DC2626",
            "line-opacity": 1,
            "line-width": 4
        }
    };
}

function crearRelleno(capa) {
    return {
        id: `${capa.nombre}-relleno`,
        type: "fill",
        source: capa.nombre,
        "source-layer": capa.nombre,
        minzoom: capa.minZoom,
        layout: { visibility: capa.visible ? "visible" : "none" },
        paint: {
            "fill-color": capa.color,
            "fill-opacity": capa.nombre === "edificaciones" ? 0.45 : 0.12
        }
    };
}

function crearLinea(capa) {
    const esVia = capa.nombre === "vias";
    return {
        id: `${capa.nombre}-linea`,
        type: "line",
        source: capa.nombre,
        "source-layer": capa.nombre,
        minzoom: capa.minZoom,
        layout: {
            visibility: capa.visible ? "visible" : "none",
            "line-cap": "round",
            "line-join": "round"
        },
        paint: {
            "line-color": capa.color,
            "line-opacity": 0.9,
            "line-width": esVia ? ["interpolate", ["linear"], ["zoom"], 13, 1, 18, 4] : 1.1
        }
    };
}

function crearEtiqueta(capa) {
    return {
        id: `${capa.nombre}-etiqueta`,
        type: "symbol",
        source: capa.nombre,
        "source-layer": capa.nombre,
        minzoom: capa.minZoomEtiqueta ?? capa.minZoom,
        layout: {
            visibility: capa.visible ? "visible" : "none",
            "text-field": ["coalesce", ["get", capa.campoEtiqueta], ""],
            "text-size": 12,
            "symbol-placement": capa.nombre === "vias" ? "line" : "point"
        },
        paint: {
            "text-color": "#0F172A",
            "text-halo-color": "#FFFFFF",
            "text-halo-width": 1.5
        }
    };
}
