const urlModulo = process.argv[2] ?? "http://localhost/js/mapa.js";
const respuesta = await fetch(urlModulo);
const tipoContenido = respuesta.headers.get("content-type") ?? "";
const cacheControlModulo = respuesta.headers.get("cache-control") ?? "";
const etagModulo = respuesta.headers.get("etag") ?? "";

console.log(`mapa_js_status=${respuesta.status}`);
console.log(`mapa_js_content_type=${tipoContenido}`);
console.log(`mapa_js_cache_control=${cacheControlModulo}`);
console.log(`mapa_js_etag=${etagModulo}`);

if (!respuesta.ok) {
    throw new Error(`No se pudo descargar ${urlModulo}: HTTP ${respuesta.status}.`);
}

if (tipoContenido.includes("text/html")) {
    throw new Error("mapa.js recibio el fallback HTML.");
}

if (!/(text|application)\/javascript/i.test(tipoContenido)) {
    throw new Error(`Content-Type inesperado para mapa.js: ${tipoContenido}.`);
}

if (!cacheControlModulo.split(",").map(x => x.trim()).includes("no-cache")) {
    throw new Error(`mapa.js no exige revalidación HTTP: Cache-Control=${cacheControlModulo}.`);
}

if (!etagModulo) {
    throw new Error("mapa.js no incluye ETag para revalidación condicional.");
}

const respuestaRevalidada = await fetch(urlModulo, {
    headers: { "If-None-Match": etagModulo },
});
console.log(`mapa_js_revalidacion_status=${respuestaRevalidada.status}`);
if (respuestaRevalidada.status !== 304) {
    throw new Error(`La revalidación de mapa.js devolvió HTTP ${respuestaRevalidada.status}, no 304.`);
}

const codigo = await respuesta.text();
const urlConfiguracion = new URL("../appsettings.json", urlModulo);
const respuestaConfiguracion = await fetch(urlConfiguracion);
console.log(`appsettings_status=${respuestaConfiguracion.status}`);
console.log(`appsettings_cache_control=${respuestaConfiguracion.headers.get("cache-control") ?? ""}`);
if (!respuestaConfiguracion.ok) {
    throw new Error(`No se pudo descargar ${urlConfiguracion}: HTTP ${respuestaConfiguracion.status}.`);
}
if (!(respuestaConfiguracion.headers.get("cache-control") ?? "").includes("no-cache")) {
    throw new Error("appsettings.json no exige revalidación HTTP.");
}

const urlCss = new URL("../css/app.css", urlModulo);
const respuestaCss = await fetch(urlCss);
console.log(`app_css_status=${respuestaCss.status}`);
console.log(`app_css_content_type=${respuestaCss.headers.get("content-type") ?? ""}`);
console.log(`app_css_cache_control=${respuestaCss.headers.get("cache-control") ?? ""}`);
if (!respuestaCss.ok || !(respuestaCss.headers.get("cache-control") ?? "").includes("no-cache")) {
    throw new Error("app.css no está disponible con revalidación HTTP obligatoria.");
}

const urlIndice = new URL("../", urlModulo);
const respuestaIndice = await fetch(urlIndice);
console.log(`index_status=${respuestaIndice.status}`);
console.log(`index_cache_control=${respuestaIndice.headers.get("cache-control") ?? ""}`);
if (!respuestaIndice.ok || !(respuestaIndice.headers.get("cache-control") ?? "").includes("no-cache")) {
    throw new Error("La respuesta SPA no exige revalidación HTTP.");
}

const configuracion = await respuestaConfiguracion.json();
const limitesConfigurados = configuracion?.Visor?.Mapa?.Limites;
if (!Array.isArray(limitesConfigurados) || limitesConfigurados.length !== 4) {
    throw new Error("appsettings.json no contiene Visor:Mapa:Limites con cuatro valores.");
}

const fuentes = [];
const capasDibujadas = [];
const encuadres = [];
const observadores = [];
const framesPendientes = new Map();
const contenedor = { clientWidth: 0, clientHeight: 0 };
let siguienteFrame = 1;

class ResizeObserverFalso {
    constructor(callback) {
        this.callback = callback;
        this.desconectado = false;
        observadores.push(this);
    }

    observe(elemento) {
        this.elemento = elemento;
    }

    disconnect() {
        this.desconectado = true;
    }

    notificar() {
        if (!this.desconectado) {
            this.callback([{ target: this.elemento }]);
        }
    }
}

function ejecutarFramesPendientes() {
    const frames = [...framesPendientes.values()];
    framesPendientes.clear();
    for (const callback of frames) {
        callback(0);
    }
}

class MapaFalso {
    constructor(opciones) {
        this.eventos = new Map();
        this.centro = { lng: 0, lat: 0 };
        this.zoom = 0;
        this.contenedor = contenedor;
    }

    addControl() {}
    isStyleLoaded() { return true; }
    on(nombre, callback) { this.eventos.set(nombre, callback); }
    once(nombre, callback) { this.eventos.set(nombre, callback); }
    addSource(nombre) { fuentes.push(nombre); }
    addLayer(capa) { capasDibujadas.push(capa.id); }
    getLayer() { return undefined; }
    resize() {}
    fitBounds(limites, opciones) {
        const w = this.contenedor.clientWidth;
        const h = this.contenedor.clientHeight;
        this.centro = {
            lng: (limites[0][0] + limites[1][0]) / 2,
            lat: (limites[0][1] + limites[1][1]) / 2,
        };
        this.zoom = w > 0 && h > 0 ? Math.min(opciones.maxZoom, 13) : 0;
        encuadres.push({ limites, opciones, w, h, zoom: this.zoom });
    }
    jumpTo(opciones) {
        this.centro = { lng: opciones.center[0], lat: opciones.center[1] };
        this.zoom = opciones.zoom;
    }
    getCenter() { return this.centro; }
    getZoom() { return this.zoom; }
    remove() {}
}

globalThis.window = {
    location: { origin: "http://localhost" },
    ResizeObserver: ResizeObserverFalso,
    requestAnimationFrame: (callback) => {
        const id = siguienteFrame++;
        framesPendientes.set(id, callback);
        return id;
    },
    cancelAnimationFrame: (id) => framesPendientes.delete(id),
    maplibregl: {
        Map: MapaFalso,
        NavigationControl: class {},
    },
};
globalThis.document = {
    getElementById: (id) => id === "mapa-sonda" ? contenedor : null,
};

const urlDatos = `data:text/javascript;base64,${Buffer.from(codigo).toString("base64")}`;
const modulo = await import(urlDatos);
const capas = [
    ["distritos", 9, true, true, "nombre"],
    ["zonas", 11, true, true, "nombre_zona"],
    ["manzanas", 13, true, true, null],
    ["parcelas", 15, true, true, null],
    ["predios-no-fotografiados", 16, true, true, null],
    ["edificaciones", 16, true, true, null],
    ["vias", 13, false, true, "nombre"],
].map(([nombre, minZoom, tieneRelleno, tieneLinea, campoEtiqueta]) => ({
    nombre,
    minZoom,
    color: "#000000",
    tieneRelleno,
    tieneLinea,
    campoEtiqueta,
    minZoomEtiqueta: minZoom,
    visible: true,
}));

modulo.crearMapa(
    "mapa-sonda",
    limitesConfigurados,
    null,
    "token-sonda",
    capas,
    { invokeMethodAsync() {} });

console.log(`dimensiones_iniciales=${contenedor.clientWidth}x${contenedor.clientHeight}`);
console.log(`encuadres_antes_layout=${encuadres.length}`);
console.log(`fuentes_antes_layout=${fuentes.length}`);

if (encuadres.length !== 0 || fuentes.length !== 0 || capasDibujadas.length !== 0) {
    throw new Error(
        `El mapa se inicializó antes del layout: encuadres=${encuadres.length}, `
        + `fuentes=${fuentes.length}, capas=${capasDibujadas.length}.`);
}

ejecutarFramesPendientes();
if (encuadres.length !== 0) {
    throw new Error("El mapa se encuadró durante un frame que todavía tenía dimensiones 0x0.");
}

contenedor.clientWidth = 1024;
contenedor.clientHeight = 768;
for (const observador of observadores) {
    observador.notificar();
}
ejecutarFramesPendientes();

console.log(`capas_entrada=${capas.length}`);
console.log(`fuentes_agregadas=${fuentes.length}`);
console.log(`capas_dibujadas=${capasDibujadas.length}`);
console.log(`bbox_sonda=${JSON.stringify(limitesConfigurados)}`);
console.log(`encuadres_aplicados=${encuadres.length}`);

if (fuentes.length !== 7) {
    throw new Error(`Se esperaban 7 fuentes y se agregaron ${fuentes.length}.`);
}

if (capasDibujadas.length !== 16) {
    throw new Error(`Se esperaban 16 capas de dibujo y se agregaron ${capasDibujadas.length}.`);
}

if (encuadres.length !== 1) {
    throw new Error(`Se esperaba 1 encuadre explícito y se aplicaron ${encuadres.length}.`);
}

const encuadre = encuadres[0];
const limitesEsperados = [
    [limitesConfigurados[0], limitesConfigurados[1]],
    [limitesConfigurados[2], limitesConfigurados[3]],
];
console.log(`encuadre_limites=${JSON.stringify(encuadre.limites)}`);
console.log(`encuadre_opciones=${JSON.stringify(encuadre.opciones)}`);
console.log(`encuadre_dimensiones=${encuadre.w}x${encuadre.h}`);
console.log(`zoom_final=${encuadre.zoom}`);
if (JSON.stringify(encuadre.limites) !== JSON.stringify(limitesEsperados)) {
    throw new Error(`Bbox inesperado: ${JSON.stringify(encuadre.limites)}.`);
}

if (encuadre.opciones.padding !== 40
    || encuadre.opciones.maxZoom !== 14
    || encuadre.opciones.duration !== 0) {
    throw new Error(`Opciones de encuadre inesperadas: ${JSON.stringify(encuadre.opciones)}.`);
}

if (encuadre.w !== 1024 || encuadre.h !== 768 || encuadre.zoom <= 10) {
    throw new Error(
        `Encuadre inválido tras adquirir layout: ${encuadre.w}x${encuadre.h}, zoom=${encuadre.zoom}.`);
}
