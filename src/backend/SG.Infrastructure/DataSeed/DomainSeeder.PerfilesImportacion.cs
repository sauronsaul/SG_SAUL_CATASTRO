using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.DataSeed;

public static partial class DomainSeeder
{
    public static async Task SeedPerfilesImportacionAsync(ApplicationDbContext db, ILogger logger)
    {
        var existente = await db.PerfilesImportacion.AnyAsync();
        if (existente)
            return;

        LogSeedPerfilesInicio(logger);

        db.PerfilesImportacion.Add(SeedPerfilPrediosUyuni());
        db.PerfilesImportacion.Add(SeedPerfilConstruccionesUyuni());

        await db.SaveChangesAsync();
        LogSeedPerfilesFin(logger);
    }

    private static PerfilImportacion SeedPerfilPrediosUyuni()
    {
        // Nombres de campo verificados con el .dbf real del catastro de Uyuni (Potosí, Bolivia).
        // cod_uv / cod_man / cod_pred son Integer64 en el .dbf; el mapeador debe
        // convertir entero→string al asignar Zona/Manzana/Lote (string en UbicacionCatastral).
        var perfil = PerfilImportacion.Crear(
            "uyuni-predios",
            TipoCapa.Predios,
            "pre_uyu_sis.shp",
            "Perfil para shapefiles de predios del municipio de Uyuni (Potosí, Bolivia).");

        // Obligatorios
        perfil.AgregarMapeo("cod_uv",     "UbicacionCatastral.Zona",      esObligatorio: true);
        perfil.AgregarMapeo("cod_man",    "UbicacionCatastral.Manzana",   esObligatorio: true);
        perfil.AgregarMapeo("cod_pred",   "UbicacionCatastral.Lote",      esObligatorio: true);
        perfil.AgregarMapeo("superficie", "SuperficieDeclarada",          esObligatorio: true);

        // Opcionales
        perfil.AgregarMapeo("cod_geo",    "CodigoOrigen",                  esObligatorio: false);
        perfil.AgregarMapeo("dir_bar",    "UbicacionCatastral.Barrio",     esObligatorio: false);
        perfil.AgregarMapeo("nompro",     "PropietarioReferencia",         esObligatorio: false);
        perfil.AgregarMapeo("tip_inm",    "TipoInmuebleOrigen",            esObligatorio: false);

        // Sin equivalencias de valor — no se mapea ninguna columna a UsoSueloId.

        return perfil;
    }

    private static PerfilImportacion SeedPerfilConstruccionesUyuni()
    {
        // Nombres de campo verificados con el .dbf real del catastro de Uyuni.
        // id_edif, cod_geo y edi_esp no se mapean (edi_esp viene vacío en todo el .dbf).
        var perfil = PerfilImportacion.Crear(
            "uyuni-construcciones",
            TipoCapa.Construcciones,
            "edi_uyu_sis.shp",
            "Perfil para shapefiles de construcciones del municipio de Uyuni (Potosí, Bolivia).");

        // Vínculo al predio: la tripleta (cod_uv, cod_man, cod_pred) identifica
        // la UbicacionCatastral del predio al que pertenece la construcción.
        perfil.AgregarMapeo("cod_uv",   "VinculoPredio.Zona",    esObligatorio: true);
        perfil.AgregarMapeo("cod_man",  "VinculoPredio.Manzana", esObligatorio: true);
        perfil.AgregarMapeo("cod_pred", "VinculoPredio.Lote",    esObligatorio: true);

        // Atributos de la construcción (prefijo Construccion.* — mismo convenio que UbicacionCatastral.*)
        perfil.AgregarMapeo("edi_num",  "Construccion.Numero", esObligatorio: true);
        perfil.AgregarMapeo("edi_piso", "Construccion.Pisos",  esObligatorio: true);
        perfil.AgregarMapeo("edi_are",  "Construccion.Area",   esObligatorio: true);

        // Opcionales
        perfil.AgregarMapeo("cod_blo",  "Construccion.Bloque", esObligatorio: false);

        return perfil;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding perfiles de importación Uyuni...")]
    private static partial void LogSeedPerfilesInicio(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Perfiles de importación sembrados correctamente.")]
    private static partial void LogSeedPerfilesFin(ILogger logger);
}
