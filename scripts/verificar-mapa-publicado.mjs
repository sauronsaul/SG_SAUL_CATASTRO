const urlModulo = process.argv[2] ?? "http://localhost/js/mapa.js";
const respuesta = await fetch(urlModulo);
const tipoContenido = respuesta.headers.get("content-type") ?? "";

console.log(`mapa_js_status=${respuesta.status}`);
console.log(`mapa_js_content_type=${tipoContenido}`);

if (!respuesta.ok) {
    throw new Error(`No se pudo descargar ${urlModulo}: HTTP ${respuesta.status}.`);
}

if (tipoContenido.includes("text/html")) {
    throw new Error("mapa.js recibio el fallback HTML.");
}

if (!/(text|application)\/javascript/i.test(tipoContenido)) {
    throw new Error(`Content-Type inesperado para mapa.js: ${tipoContenido}.`);
}

const codigo = await respuesta.text();
const urlConfiguracion = new URL("../appsettings.json", urlModulo);
const respuestaConfiguracion = await fetch(urlConfiguracion);
console.log(`appsettings_status=${respuestaConfiguracion.status}`);
if (!respuestaConfiguracion.ok) {
    throw new Error(`No se pudo descargar ${urlConfiguracion}: HTTP ${respuestaConfiguracion.status}.`);
}

const configuracion = await respuestaConfiguracion.json();
const limitesConfigurados = configuracion?.Visor?.Mapa?.Limites;
if (!Array.isArray(limitesConfigurados) || limitesConfigurados.length !== 4) {
    throw new Error("appsettings.json no contiene Visor:Mapa:Limites con cuatro valores.");
}

const fuentes = [];
const capasDibujadas = [];
const encuadres = [];

class MapaFalso {
    constructor(opciones) {
        this.eventos = new Map();
        this.centro = { lng: 0, lat: 0 };
        this.zoom = 0;
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
        encuadres.push({ limites, opciones });
        this.centro = {
            lng: (limites[0][0] + limites[1][0]) / 2,
            lat: (limites[0][1] + limites[1][1]) / 2,
        };
        this.zoom = opciones.maxZoom;
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
    maplibregl: {
        Map: MapaFalso,
        NavigationControl: class {},
    },
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
if (JSON.stringify(encuadre.limites) !== JSON.stringify(limitesEsperados)) {
    throw new Error(`Bbox inesperado: ${JSON.stringify(encuadre.limites)}.`);
}

if (encuadre.opciones.padding !== 40 || encuadre.opciones.maxZoom !== 14) {
    throw new Error(`Opciones de encuadre inesperadas: ${JSON.stringify(encuadre.opciones)}.`);
}
