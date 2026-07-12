using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.DataSeed;

public static partial class DomainSeeder
{
    public static async Task SeedPerfilesImportacionAsync(ApplicationDbContext db, ILogger logger)
    {
        var existentes = await db.PerfilesImportacion
            .Select(x => x.Nombre)
            .ToListAsync();

        var perfiles = new List<PerfilImportacion>
        {
            SeedPerfilPrediosUyuni(),
            SeedPerfilConstruccionesUyuni(),
        };
        perfiles.AddRange(SeedPerfilesVersionadosUyuni());

        var nuevos = perfiles
            .Where(x => !existentes.Contains(x.Nombre, StringComparer.Ordinal))
            .ToList();
        if (nuevos.Count == 0)
            return;

        LogSeedPerfilesInicio(logger);
        db.PerfilesImportacion.AddRange(nuevos);

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

    private static IReadOnlyList<PerfilImportacion> SeedPerfilesVersionadosUyuni()
    {
        var definiciones = DefinicionesCapasVersionadasUyuni.Todas.ToDictionary(x => x.NombrePerfil);

        return
        [
            CrearPerfilVersionado(definiciones["uyuni-versionado-parcelas"],
            [
                ("cod_uv", "CapaParcela.CodUv", true), ("cod_man", "CapaParcela.CodMan", true),
                ("cod_pred", "CapaParcela.CodPred", true), ("cod_geo", "CapaParcela.CodigoGeografico", false),
                ("superficie", "CapaParcela.Superficie", false), ("val_zon", "CapaParcela.ValuacionZonal", false),
                ("tip_inm", "CapaParcela.TipoInmueble", false), ("ser_alc", "CapaParcela.ServicioAlcantarillado", false),
                ("ser_agu", "CapaParcela.ServicioAgua", false), ("ser_luz", "CapaParcela.ServicioLuz", false),
                ("ser_tel", "CapaParcela.ServicioTelefonia", false), ("nompro", "CapaParcela.NombrePropietarioOrigen", false),
                ("nomvia", "CapaParcela.NombreVia", false), ("dir_bar", "CapaParcela.DireccionBarrio", false),
                ("dir_urb", "CapaParcela.DireccionUrbana", false), ("ter_uso", "CapaParcela.UsoTerreno", false),
                ("ter_top", "CapaParcela.TopografiaTerreno", false),
            ]),
            CrearPerfilVersionado(definiciones["uyuni-versionado-edificaciones"],
            [
                ("id_edif", "CapaEdificacion.IdEdificacionOrigen", false), ("cod_geo", "CapaEdificacion.CodigoGeografico", false),
                ("cod_uv", "CapaEdificacion.CodUv", false), ("cod_man", "CapaEdificacion.CodMan", false),
                ("cod_pred", "CapaEdificacion.CodPred", false), ("edi_num", "CapaEdificacion.NumeroEdificacion", false),
                ("edi_piso", "CapaEdificacion.Piso", false), ("edi_esp", "CapaEdificacion.CodigoEspacio", false),
                ("cod_blo", "CapaEdificacion.CodigoBloque", false), ("edi_are", "CapaEdificacion.AreaConstruida", false),
            ]),
            CrearPerfilVersionado(definiciones["uyuni-versionado-predios-no-fotografiados"],
            [
                ("id_predio", "CapaPredioNoFotografiado.IdPredioOrigen", false), ("cod_geo", "CapaPredioNoFotografiado.CodigoGeografico", false),
                ("cod_uv", "CapaPredioNoFotografiado.CodUv", false), ("cod_man", "CapaPredioNoFotografiado.CodMan", false),
                ("cod_pred", "CapaPredioNoFotografiado.CodPred", false), ("fotos", "CapaPredioNoFotografiado.IndicadorFotos", false),
                ("FOT_FREN", "CapaPredioNoFotografiado.FotoFrente", false), ("FOT_DER", "CapaPredioNoFotografiado.FotoDerecha", false),
                ("FOT_IZ", "CapaPredioNoFotografiado.FotoIzquierda", false),
            ]),
            CrearPerfilVersionado(definiciones["uyuni-versionado-manzanas"],
            [
                ("cod_geo", "CapaManzana.CodigoGeografico", false), ("cod_uv", "CapaManzana.CodUv", false),
                ("cod_man", "CapaManzana.CodMan", false), ("Coordenada", "CapaManzana.CoordenadaOrigen", false),
            ]),
            CrearPerfilVersionado(definiciones["uyuni-versionado-distritos"],
            [
                ("cod_geo", "CapaDistrito.CodigoGeografico", false), ("cod_uv", "CapaDistrito.CodUv", false),
                ("nombre", "CapaDistrito.Nombre", false),
            ]),
            CrearPerfilVersionado(definiciones["uyuni-versionado-zonas"],
            [
                ("zona", "CapaZona.NombreZona", false), ("id_zona", "CapaZona.IdZonaOrigen", false),
                ("cod_geo", "CapaZona.CodigoGeografico", false),
            ]),
            CrearPerfilVersionado(definiciones["uyuni-versionado-vias"],
            [
                ("MATERIAL", "CapaVia.Material", false), ("NOMBRE", "CapaVia.Nombre", false),
                ("TIPO", "CapaVia.Tipo", false), ("Distancia", "CapaVia.DistanciaOrigen", false),
            ]),
        ];
    }

    private static PerfilImportacion CrearPerfilVersionado(
        DefinicionCapaVersionadaUyuni definicion,
        IReadOnlyList<(string Origen, string Destino, bool Obligatorio)> mapeos)
    {
        var perfil = PerfilImportacion.Crear(
            definicion.NombrePerfil,
            definicion.TipoCapa,
            definicion.NombreArchivoShp,
            $"Perfil versionado para {definicion.NombreTabla} de Uyuni.");

        foreach (var (origen, destino, obligatorio) in mapeos)
            perfil.AgregarMapeo(origen, destino, obligatorio);

        return perfil;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding perfiles de importación Uyuni...")]
    private static partial void LogSeedPerfilesInicio(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Perfiles de importación sembrados correctamente.")]
    private static partial void LogSeedPerfilesFin(ILogger logger);
}
