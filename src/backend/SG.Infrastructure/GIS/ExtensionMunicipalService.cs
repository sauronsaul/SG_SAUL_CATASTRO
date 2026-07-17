using System.Data;
using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.GIS;
using SG.Contracts.GIS;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.GIS;

internal sealed class ExtensionMunicipalService(ApplicationDbContext db)
    : IExtensionMunicipalService
{
    private const string Sql = """
        WITH geometrias AS (
            SELECT p.geometria
            FROM dominio.capa_parcelas p
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'Predios' AND NOT e.is_deleted
            WHERE p.dataset_version_id = @version AND p.geometria IS NOT NULL
            UNION ALL
            SELECT c.geometria FROM dominio.capa_edificaciones c
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'Construcciones' AND NOT e.is_deleted
            WHERE c.dataset_version_id = @version AND c.geometria IS NOT NULL
            UNION ALL
            SELECT n.geometria FROM dominio.capa_predios_no_fotografiados n
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'PrediosNoFotografiados' AND NOT e.is_deleted
            WHERE n.dataset_version_id = @version AND n.geometria IS NOT NULL
            UNION ALL
            SELECT m.geometria FROM dominio.capa_manzanas m
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'Manzanas' AND NOT e.is_deleted
            WHERE m.dataset_version_id = @version AND m.geometria IS NOT NULL
            UNION ALL
            SELECT d.geometria FROM dominio.capa_distritos d
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'Distritos' AND NOT e.is_deleted
            WHERE d.dataset_version_id = @version AND d.geometria IS NOT NULL
            UNION ALL
            SELECT z.geometria FROM dominio.capa_zonas z
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'ZonasValuacion' AND NOT e.is_deleted
            WHERE z.dataset_version_id = @version AND z.geometria IS NOT NULL
            UNION ALL
            SELECT v.geometria FROM dominio.capa_vias v
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'Vias' AND NOT e.is_deleted
            WHERE v.dataset_version_id = @version AND v.geometria IS NOT NULL
            UNION ALL
            SELECT a.geometria FROM dominio.capa_areas_urbanas a
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'AreasUrbanas' AND NOT e.is_deleted
            WHERE a.dataset_version_id = @version AND a.geometria IS NOT NULL
            UNION ALL
            SELECT g.geometria FROM dominio.capa_puntos_geodesicos g
            JOIN dominio.esquemas_capas e
              ON e.municipio_codigo = @municipio AND e.tipo_capa = 'PuntosGeodesicos' AND NOT e.is_deleted
            WHERE g.dataset_version_id = @version AND g.geometria IS NOT NULL
        ),
        extension_32719 AS (
            SELECT ST_Extent(geometria)::geometry AS geometria
            FROM geometrias
        ),
        extension_4326 AS (
            SELECT ST_Transform(ST_SetSRID(geometria, 32719), 4326) AS geometria
            FROM extension_32719
            WHERE geometria IS NOT NULL
        )
        SELECT ST_XMin(geometria),
               ST_YMin(geometria),
               ST_XMax(geometria),
               ST_YMax(geometria)
        FROM extension_4326;
        """;

    public async Task<LimitesVisorDto?> ObtenerAsync(
        string municipioCodigo,
        Guid datasetVersionId,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var cerrarConexion = connection.State != ConnectionState.Open;
        if (cerrarConexion)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Sql;
            command.CommandTimeout = 30;
            AgregarParametro(command, "municipio", municipioCodigo, DbType.String);
            AgregarParametro(command, "version", datasetVersionId, DbType.Guid);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
                return null;

            return new LimitesVisorDto(
                reader.GetDouble(0),
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.GetDouble(3));
        }
        finally
        {
            if (cerrarConexion)
                await connection.CloseAsync();
        }
    }

    private static void AgregarParametro(
        System.Data.Common.DbCommand command,
        string nombre,
        object valor,
        DbType tipo)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = nombre;
        parameter.Value = valor;
        parameter.DbType = tipo;
        command.Parameters.Add(parameter);
    }
}
