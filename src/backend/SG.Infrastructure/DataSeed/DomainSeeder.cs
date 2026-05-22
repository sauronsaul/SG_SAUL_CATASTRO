using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SG.Domain.Catalogos;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.DataSeed;

public static partial class DomainSeeder
{
    private static readonly (string Codigo, string Nombre, string Descripcion, int Orden)[] UsosSueloCaranavi =
    [
        ("RU", "Residencial Unifamiliar",        "Vivienda unifamiliar de uso exclusivo.",                     1),
        ("RM", "Residencial Multifamiliar",      "Vivienda colectiva, edificios de departamentos.",            2),
        ("MX", "Mixto",                          "Uso mixto residencial-comercial u otros combinados.",        3),
        ("CO", "Comercial",                      "Actividades de comercio al por mayor y menor.",              4),
        ("IN", "Industrial",                     "Actividades productivas, talleres, plantas.",                5),
        ("EE", "Equipamiento Educativo",         "Unidades educativas, colegios, universidades.",              6),
        ("ES", "Equipamiento Salud",             "Centros de salud, hospitales, clínicas.",                    7),
        ("ED", "Educativo",                      "Espacios de formación y capacitación complementaria.",       8),
        ("RC", "Recreativo",                     "Canchas, polideportivos, parques de juego.",                 9),
        ("RL", "Religioso",                      "Iglesias, templos, capillas y similares.",                   10),
        ("IG", "Institucional / Gubernamental",  "Oficinas públicas, municipalidad, dependencias del Estado.", 11),
        ("TR", "Transporte",                     "Terminales, estaciones, playas de estacionamiento.",         12),
        ("ME", "Mercado",                        "Mercados municipales y ferias permanentes.",                 13),
        ("AV", "Área Verde",                     "Parques, jardines, áreas de recreación pública.",            14),
        ("OT", "Otros",                          "Uso de suelo no clasificado en las categorías anteriores.",  15),
    ];

    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger)
    {
        var existente = await db.UsosSuelo.AnyAsync();
        if (existente)
            return;

        LogSeedInicio(logger, UsosSueloCaranavi.Length);

        foreach (var (codigo, nombre, descripcion, orden) in UsosSueloCaranavi)
        {
            var result = UsoSuelo.Crear(codigo, nombre, orden, descripcion);
            if (result.IsFailure)
            {
                LogSeedError(logger, codigo, result.Error.Message);
                continue;
            }

            db.UsosSuelo.Add(result.Value);
        }

        await db.SaveChangesAsync();
        LogSeedFin(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding {Count} usos de suelo para Caranavi...")]
    private static partial void LogSeedInicio(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error creando uso de suelo '{Codigo}': {Error}")]
    private static partial void LogSeedError(ILogger logger, string codigo, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usos de suelo sembrados correctamente.")]
    private static partial void LogSeedFin(ILogger logger);
}
