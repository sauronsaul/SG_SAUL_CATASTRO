using System.Globalization;
using SG.Application.Abstractions;
using SG.Domain.Importacion;

namespace SG.Application.Importacion;

internal sealed class MapeadorImportacion : IMapeadorImportacion
{
    // Campos DESTINO del dominio que constituyen la tripleta de identidad del predio.
    // El rechazo se evalúa sobre el campo destino (agnóstico al municipio), no sobre
    // el nombre de columna origen del shapefile.
    private static readonly HashSet<string> CamposTripleta = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tripleta de identidad para capa de predios.
        "UbicacionCatastral.Zona",
        "UbicacionCatastral.Manzana",
        "UbicacionCatastral.Lote",
        // Tripleta de vínculo para capa de construcciones (referencia al predio padre).
        "VinculoPredio.Zona",
        "VinculoPredio.Manzana",
        "VinculoPredio.Lote",
    };

    public ResultadoMapeoFila Mapear(
        RegistroCrudoShapefile registro,
        PerfilImportacion perfil,
        int numeroFila)
    {
        var erroresRechazo = new List<string>();
        var advertencias = new List<string>();
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Geometría ausente o vacía = rechazo irrecuperable.
        if (registro.Geometria is null)
            erroresRechazo.Add(registro.ErrorGeometria is not null
                ? $"Geometría inválida: {registro.ErrorGeometria}"
                : "La fila no tiene geometría y no puede importarse.");
        else if (registro.Geometria.IsEmpty)
            erroresRechazo.Add("La fila tiene una geometría vacía y no puede importarse.");

        if (registro.ProyeccionDesconocida)
            advertencias.Add("Proyección desconocida: la geometría requiere revisión manual.");

        foreach (var mapeo in perfil.Mapeos)
        {
            if (!registro.Atributos.TryGetValue(mapeo.NombreColumnaOrigen, out var valorRaw)
                || valorRaw is null or DBNull)
            {
                // Tripleta ausente = rechazo (no se puede identificar el predio).
                // Obligatorio no-tripleta ausente = advertencia (entra para revisión).
                // Opcional ausente = advertencia informativa.
                if (CamposTripleta.Contains(mapeo.CampoDestino))
                    erroresRechazo.Add(
                        $"Campo de identidad '{mapeo.CampoDestino}' " +
                        $"(columna '{mapeo.NombreColumnaOrigen}') ausente o nulo.");
                else if (mapeo.EsObligatorio)
                    advertencias.Add(
                        $"Campo obligatorio '{mapeo.NombreColumnaOrigen}' ausente — fila marcada para revisión.");
                else
                    advertencias.Add($"Campo opcional '{mapeo.NombreColumnaOrigen}' ausente.");
                continue;
            }

            var valorStr = ConvertirAString(valorRaw);

            // Resolver equivalencia si la hay (case-insensitive).
            var eq = mapeo.Equivalencias
                .FirstOrDefault(e => string.Equals(e.ValorOrigen, valorStr, StringComparison.OrdinalIgnoreCase));
            if (eq is not null)
                valorStr = eq.ValorDestino;

            valores[mapeo.CampoDestino] = valorStr;
        }

        var clasificacion = erroresRechazo.Count > 0
            ? ClasificacionFila.Rechazada
            : advertencias.Count > 0
                ? ClasificacionFila.Advertencia
                : ClasificacionFila.Ok;

        return new ResultadoMapeoFila(
            numeroFila,
            clasificacion,
            valores,
            registro.Geometria,
            advertencias,
            erroresRechazo);
    }

    // Integer64 del DBF llega como long en NTS.IO.Esri. InvariantCulture evita separadores
    // de miles o comas decimales dependientes de la cultura del servidor.
    private static string ConvertirAString(object valor) =>
        valor switch
        {
            long l    => l.ToString(CultureInfo.InvariantCulture),
            int i     => i.ToString(CultureInfo.InvariantCulture),
            double d  => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            float f   => f.ToString(CultureInfo.InvariantCulture),
            _         => valor.ToString() ?? string.Empty,
        };
}
