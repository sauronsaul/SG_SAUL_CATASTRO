using System.Data;
using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.GIS;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.GIS;

public sealed class TileVectorialService(ApplicationDbContext db) : ITileVectorialService
{
    public async Task<TileVectorialPersistido?> ObtenerAsync(
        string municipioCodigo,
        TipoCapa capa,
        string nombreCapa,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var cerrarConexion = connection.State != ConnectionState.Open;
        if (cerrarConexion)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ObtenerSql(capa);
            command.CommandTimeout = 30;
            AgregarParametro(command, "municipio", municipioCodigo, DbType.String);
            AgregarParametro(command, "nombre_capa", nombreCapa, DbType.String);
            AgregarParametro(command, "z", z, DbType.Int32);
            AgregarParametro(command, "x", x, DbType.Int32);
            AgregarParametro(command, "y", y, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new TileVectorialPersistido(
                reader.GetGuid(0),
                reader.GetInt32(1),
                reader.GetFieldValue<byte[]>(2));
        }
        finally
        {
            if (cerrarConexion)
                await connection.CloseAsync();
        }
    }

    private static string ObtenerSql(TipoCapa capa)
    {
        var (tabla, atributos) = capa switch
        {
            TipoCapa.Predios => ("capa_parcelas", "c.cod_uv, c.cod_man, c.cod_pred,"),
            TipoCapa.Construcciones => ("capa_edificaciones", "c.cod_uv, c.cod_man, c.cod_pred, c.numero_edificacion, c.piso, c.codigo_bloque,"),
            TipoCapa.PrediosNoFotografiados => ("capa_predios_no_fotografiados", "c.cod_uv, c.cod_man, c.cod_pred,"),
            TipoCapa.Manzanas => ("capa_manzanas", "c.cod_uv, c.cod_man,"),
            TipoCapa.Distritos => ("capa_distritos", "c.cod_uv, c.nombre,"),
            TipoCapa.ZonasValuacion => ("capa_zonas", "c.nombre_zona,"),
            TipoCapa.Vias => ("capa_vias", "c.nombre, c.tipo, c.material,"),
            TipoCapa.AreasUrbanas => ("capa_areas_urbanas", ""),
            TipoCapa.PuntosGeodesicos => ("capa_puntos_geodesicos", "c.atributos_extra ->> 'PUNTOS' AS puntos,"),
            _ => throw new ArgumentOutOfRangeException(nameof(capa), capa, null),
        };

        return $"""
            WITH version_activa AS MATERIALIZED (
                SELECT id, numero_version
                FROM dominio.dataset_versiones
                WHERE municipio_codigo = @municipio AND estado = 'Activa'
                LIMIT 1
            ),
            limites AS (
                SELECT envolvente_3857,
                       ST_Transform(envolvente_3857, 32719) AS envolvente_32719
                FROM (SELECT ST_TileEnvelope(@z, @x, @y) AS envolvente_3857) tile
            ),
            mvt_rows AS (
                SELECT c.fila_origen AS id,
                       {atributos}
                       ST_AsMVTGeom(
                           ST_Transform(c.geometria, 3857),
                           l.envolvente_3857,
                           4096,
                           64,
                           true) AS geom
                FROM dominio.{tabla} c
                JOIN version_activa v ON v.id = c.dataset_version_id
                CROSS JOIN limites l
                WHERE c.geometria IS NOT NULL
                  AND c.geometria && l.envolvente_32719
                  AND ST_Intersects(c.geometria, l.envolvente_32719)
            )
            SELECT v.id,
                   v.numero_version,
                   COALESCE(
                       (SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows),
                       '\x'::bytea)
            FROM version_activa v;
            """;
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
