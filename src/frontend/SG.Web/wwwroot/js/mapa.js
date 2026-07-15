const mapas = new Map();
const prefijoLog = "[SG.Web mapa]";

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
        transformRequest: (url) => transformarSolicitud(url, token)
    };

    const mapa = new window.maplibregl.Map(opciones);
    mapa.addControl(new window.maplibregl.NavigationControl({ showCompass: false }), "top-right");

    const estado = { mapa, notificado401: false, capasInicializadas: false, referenciaDotNet };
    mapas.set(contenedorId, estado);

    mapa.on("load", () => {
        console.info(`${prefijoLog} evento load`);
    });

    const agregarCapas = () => {
        if (estado.capasInicializadas) return;
        estado.capasInicializadas = true;
        aplicarEncuadre(mapa, limites, camara);
        console.info(`${prefijoLog} inicialización de capas`, { cantidadCapas: capas.length });

        for (const capa of capas) {
            console.info(`${prefijoLog} addSource`, { capa: capa.nombre, minZoom: capa.minZoom });
            mapa.addSource(capa.nombre, {
                type: "vector",
                tiles: [`/api/tiles/${capa.nombre}/{z}/{x}/{y}.mvt`],
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

        console.info(`${prefijoLog} capas listas`, {
            fuentes: capas.length,
            capasDibujadas: capas.filter(x => x.tieneRelleno).length
                + capas.filter(x => x.tieneLinea).length
                + capas.filter(x => x.campoEtiqueta).length
        });
    };

    if (mapa.isStyleLoaded()) {
        console.info(`${prefijoLog} estilo ya cargado; inicialización inmediata`);
        agregarCapas();
    } else {
        console.info(`${prefijoLog} esperando evento style.load`);
        mapa.once("style.load", agregarCapas);
    }

    mapa.on("error", (evento) => {
        const status = evento?.error?.status;
        if (status === 401 && !estado.notificado401) {
            estado.notificado401 = true;
            void referenciaDotNet.invokeMethodAsync("NotificarNoAutorizadoAsync");
        }
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

function aplicarEncuadre(mapa, limites, camara) {
    mapa.resize();

    if (camara) {
        mapa.jumpTo({
            center: [camara.longitud, camara.latitud],
            zoom: camara.zoom
        });
    } else {
        mapa.fitBounds(
            [[limites[0], limites[1]], [limites[2], limites[3]]],
            { padding: 40, maxZoom: 14 });
    }

    const centro = mapa.getCenter();
    console.info(`${prefijoLog} encuadre aplicado`, {
        center: [centro.lng, centro.lat],
        zoom: mapa.getZoom()
    });
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

export function destruirMapa(contenedorId) {
    const estado = mapas.get(contenedorId);
    if (!estado) return;
    estado.mapa.remove();
    mapas.delete(contenedorId);
}

function transformarSolicitud(url, token) {
    const destino = new URL(url, window.location.origin);
    if (destino.origin === window.location.origin && destino.pathname.startsWith("/api/tiles/")) {
        return { url, headers: { Authorization: `Bearer ${token}` } };
    }
    return { url };
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
