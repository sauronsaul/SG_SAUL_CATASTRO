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
const fuentes = [];
const capasDibujadas = [];

class MapaFalso {
    constructor() {
        this.eventos = new Map();
    }

    addControl() {}
    isStyleLoaded() { return true; }
    on(nombre, callback) { this.eventos.set(nombre, callback); }
    once(nombre, callback) { this.eventos.set(nombre, callback); }
    addSource(nombre) { fuentes.push(nombre); }
    addLayer(capa) { capasDibujadas.push(capa.id); }
    getLayer() { return undefined; }
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
    [-66.85, -20.49, -66.80, -20.44],
    null,
    "token-sonda",
    capas,
    { invokeMethodAsync() {} });

console.log(`capas_entrada=${capas.length}`);
console.log(`fuentes_agregadas=${fuentes.length}`);
console.log(`capas_dibujadas=${capasDibujadas.length}`);

if (fuentes.length !== 7) {
    throw new Error(`Se esperaban 7 fuentes y se agregaron ${fuentes.length}.`);
}

if (capasDibujadas.length !== 16) {
    throw new Error(`Se esperaban 16 capas de dibujo y se agregaron ${capasDibujadas.length}.`);
}
