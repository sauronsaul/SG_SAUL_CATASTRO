using SG.Application.Abstractions;
using SG.Contracts.Importacion;
using SG.Domain.Catastro.Enums;

namespace SG.Application.Importacion.GenerarPreview;

// Lógica de decisión del preview extraída para testabilidad directa desde
// SG.Application.Tests (InternalsVisibleTo en Properties/AssemblyInfo.cs).
internal static class ClasificadorAccionPreview
{
    internal static AccionPreviewFila Clasificar(
        ResultadoMapeoFila resultado,
        IReadOnlyDictionary<(string Zona, string Manzana, string Lote), EstadoPredio> existentes)
    {
        if (resultado.Clasificacion == ClasificacionFila.Rechazada)
            return AccionPreviewFila.Rechazada;

        var tripleta = ExtraerTripleta(resultado.ValoresMapeados);
        if (tripleta is null)
            return AccionPreviewFila.Rechazada;

        if (!existentes.TryGetValue(tripleta.Value, out var estado))
            return AccionPreviewFila.Crear;

        // Importado y Borrador son estados mutables: la importación puede sobreescribirlos.
        // EnRevision, Observado y Validado están en flujo activo: se omiten para no
        // interrumpir revisiones en curso ni pisar datos ya validados.
        return estado is EstadoPredio.Importado or EstadoPredio.Borrador
            ? AccionPreviewFila.Actualizar
            : AccionPreviewFila.Omitir;
    }

    // Extrae la tripleta (Zona, Manzana, Lote) de los valores mapeados de una fila.
    // Devuelve null si algún componente falta (la fila se tratará como Rechazada en el
    // clasificador — situación que ya detecta el MapeadorImportacion para la tripleta).
    internal static (string Zona, string Manzana, string Lote)?
        ExtraerTripleta(IReadOnlyDictionary<string, string?> valores)
    {
        if (!valores.TryGetValue("UbicacionCatastral.Zona", out var zona) || zona is null)
            return null;
        if (!valores.TryGetValue("UbicacionCatastral.Manzana", out var manzana) || manzana is null)
            return null;
        if (!valores.TryGetValue("UbicacionCatastral.Lote", out var lote) || lote is null)
            return null;
        return (zona, manzana, lote);
    }
}
