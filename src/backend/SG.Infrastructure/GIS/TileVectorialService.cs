using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SG.Application.Abstractions.GIS;
using SG.Application.GIS.Tiles;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.GIS;

public sealed class TileVectorialService(
    ApplicationDbContext db,
    IOptions<TilesSettings> settings) : ITileVectorialService
{
    private const string SqlParcelas = """
        WITH version_activa AS MATERIALIZED (
            SELECT id
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
                   c.cod_uv,
                   c.cod_man,
                   c.cod_pred,
                   ST_AsMVTGeom(ST_Transform(c.geometria, 3857), l.envolvente_3857, 4096, 64, true) AS geom
            FROM dominio.capa_parcelas c
            JOIN version_activa v ON v.id = c.dataset_version_id
            CROSS JOIN limites l
            WHERE c.geometria IS NOT NULL
              AND c.geometria && l.envolvente_32719
              AND ST_Intersects(c.geometria, l.envolvente_32719)
        )
        SELECT v.id,
               COALESCE((SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows), '\x'::bytea)
        FROM version_activa v;
        """;

    private const string SqlEdificaciones = """
        WITH version_activa AS MATERIALIZED (
            SELECT id FROM dominio.dataset_versiones
            WHERE municipio_codigo = @municipio AND estado = 'Activa' LIMIT 1
        ),
        limites AS (
            SELECT envolvente_3857, ST_Transform(envolvente_3857, 32719) AS envolvente_32719
            FROM (SELECT ST_TileEnvelope(@z, @x, @y) AS envolvente_3857) tile
        ),
        mvt_rows AS (
            SELECT c.fila_origen AS id, c.cod_uv, c.cod_man, c.cod_pred,
                   c.numero_edificacion, c.piso, c.codigo_bloque,
                   ST_AsMVTGeom(ST_Transform(c.geometria, 3857), l.envolvente_3857, 4096, 64, true) AS geom
            FROM dominio.capa_edificaciones c
            JOIN version_activa v ON v.id = c.dataset_version_id
            CROSS JOIN limites l
            WHERE c.geometria IS NOT NULL
              AND c.geometria && l.envolvente_32719
              AND ST_Intersects(c.geometria, l.envolvente_32719)
        )
        SELECT v.id,
               COALESCE((SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows), '\x'::bytea)
        FROM version_activa v;
        """;

    private const string SqlPrediosNoFotografiados = """
        WITH version_activa AS MATERIALIZED (
            SELECT id FROM dominio.dataset_versiones
            WHERE municipio_codigo = @municipio AND estado = 'Activa' LIMIT 1
        ),
        limites AS (
            SELECT envolvente_3857, ST_Transform(envolvente_3857, 32719) AS envolvente_32719
            FROM (SELECT ST_TileEnvelope(@z, @x, @y) AS envolvente_3857) tile
        ),
        mvt_rows AS (
            SELECT c.fila_origen AS id, c.cod_uv, c.cod_man, c.cod_pred,
                   ST_AsMVTGeom(ST_Transform(c.geometria, 3857), l.envolvente_3857, 4096, 64, true) AS geom
            FROM dominio.capa_predios_no_fotografiados c
            JOIN version_activa v ON v.id = c.dataset_version_id
            CROSS JOIN limites l
            WHERE c.geometria IS NOT NULL
              AND c.geometria && l.envolvente_32719
              AND ST_Intersects(c.geometria, l.envolvente_32719)
        )
        SELECT v.id,
               COALESCE((SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows), '\x'::bytea)
        FROM version_activa v;
        """;

    private const string SqlManzanas = """
        WITH version_activa AS MATERIALIZED (
            SELECT id FROM dominio.dataset_versiones
            WHERE municipio_codigo = @municipio AND estado = 'Activa' LIMIT 1
        ),
        limites AS (
            SELECT envolvente_3857, ST_Transform(envolvente_3857, 32719) AS envolvente_32719
            FROM (SELECT ST_TileEnvelope(@z, @x, @y) AS envolvente_3857) tile
        ),
        mvt_rows AS (
            SELECT c.fila_origen AS id, c.cod_uv, c.cod_man,
                   ST_AsMVTGeom(ST_Transform(c.geometria, 3857), l.envolvente_3857, 4096, 64, true) AS geom
            FROM dominio.capa_manzanas c
            JOIN version_activa v ON v.id = c.dataset_version_id
            CROSS JOIN limites l
            WHERE c.geometria IS NOT NULL
              AND c.geometria && l.envolvente_32719
              AND ST_Intersects(c.geometria, l.envolvente_32719)
        )
        SELECT v.id,
               COALESCE((SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows), '\x'::bytea)
        FROM version_activa v;
        """;

    private const string SqlDistritos = """
        WITH version_activa AS MATERIALIZED (
            SELECT id FROM dominio.dataset_versiones
            WHERE municipio_codigo = @municipio AND estado = 'Activa' LIMIT 1
        ),
        limites AS (
            SELECT envolvente_3857, ST_Transform(envolvente_3857, 32719) AS envolvente_32719
            FROM (SELECT ST_TileEnvelope(@z, @x, @y) AS envolvente_3857) tile
        ),
        mvt_rows AS (
            SELECT c.fila_origen AS id, c.cod_uv, c.nombre,
                   ST_AsMVTGeom(ST_Transform(c.geometria, 3857), l.envolvente_3857, 4096, 64, true) AS geom
            FROM dominio.capa_distritos c
            JOIN version_activa v ON v.id = c.dataset_version_id
            CROSS JOIN limites l
            WHERE c.geometria IS NOT NULL
              AND c.geometria && l.envolvente_32719
              AND ST_Intersects(c.geometria, l.envolvente_32719)
        )
        SELECT v.id,
               COALESCE((SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows), '\x'::bytea)
        FROM version_activa v;
        """;

    private const string SqlZonas = """
        WITH version_activa AS MATERIALIZED (
            SELECT id FROM dominio.dataset_versiones
            WHERE municipio_codigo = @municipio AND estado = 'Activa' LIMIT 1
        ),
        limites AS (
            SELECT envolvente_3857, ST_Transform(envolvente_3857, 32719) AS envolvente_32719
            FROM (SELECT ST_TileEnvelope(@z, @x, @y) AS envolvente_3857) tile
        ),
        mvt_rows AS (
            SELECT c.fila_origen AS id, c.nombre_zona,
                   ST_AsMVTGeom(ST_Transform(c.geometria, 3857), l.envolvente_3857, 4096, 64, true) AS geom
            FROM dominio.capa_zonas c
            JOIN version_activa v ON v.id = c.dataset_version_id
            CROSS JOIN limites l
            WHERE c.geometria IS NOT NULL
              AND c.geometria && l.envolvente_32719
              AND ST_Intersects(c.geometria, l.envolvente_32719)
        )
        SELECT v.id,
               COALESCE((SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows), '\x'::bytea)
        FROM version_activa v;
        """;

    private const string SqlVias = """
        WITH version_activa AS MATERIALIZED (
            SELECT id FROM dominio.dataset_versiones
            WHERE municipio_codigo = @municipio AND estado = 'Activa' LIMIT 1
        ),
        limites AS (
            SELECT envolvente_3857, ST_Transform(envolvente_3857, 32719) AS envolvente_32719
            FROM (SELECT ST_TileEnvelope(@z, @x, @y) AS envolvente_3857) tile
        ),
        mvt_rows AS (
            SELECT c.fila_origen AS id, c.nombre, c.tipo, c.material,
                   ST_AsMVTGeom(ST_Transform(c.geometria, 3857), l.envolvente_3857, 4096, 64, true) AS geom
            FROM dominio.capa_vias c
            JOIN version_activa v ON v.id = c.dataset_version_id
            CROSS JOIN limites l
            WHERE c.geometria IS NOT NULL
              AND c.geometria && l.envolvente_32719
              AND ST_Intersects(c.geometria, l.envolvente_32719)
        )
        SELECT v.id,
               COALESCE((SELECT ST_AsMVT(mvt_rows, @nombre_capa, 4096, 'geom', 'id') FROM mvt_rows), '\x'::bytea)
        FROM version_activa v;
        """;

    public async Task<TileVectorialPersistido?> ObtenerAsync(
        CapaTile capa,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        var municipio = settings.Value.MunicipioCodigo;
        if (string.IsNullOrWhiteSpace(municipio))
            throw new InvalidOperationException("Tiles:MunicipioCodigo no esta configurado.");

        var connection = db.Database.GetDbConnection();
        var cerrarConexion = connection.State != ConnectionState.Open;
        if (cerrarConexion)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ObtenerSql(capa);
            command.CommandTimeout = 30;
            AgregarParametro(command, "municipio", municipio, DbType.String);
            AgregarParametro(command, "nombre_capa", CatalogoCapasTile.ObtenerNombre(capa), DbType.String);
            AgregarParametro(command, "z", z, DbType.Int32);
            AgregarParametro(command, "x", x, DbType.Int32);
            AgregarParametro(command, "y", y, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new TileVectorialPersistido(reader.GetGuid(0), reader.GetFieldValue<byte[]>(1));
        }
        finally
        {
            if (cerrarConexion)
                await connection.CloseAsync();
        }
    }

    private static string ObtenerSql(CapaTile capa) => capa switch
    {
        CapaTile.Parcelas => SqlParcelas,
        CapaTile.Edificaciones => SqlEdificaciones,
        CapaTile.PrediosNoFotografiados => SqlPrediosNoFotografiados,
        CapaTile.Manzanas => SqlManzanas,
        CapaTile.Distritos => SqlDistritos,
        CapaTile.Zonas => SqlZonas,
        CapaTile.Vias => SqlVias,
        _ => throw new ArgumentOutOfRangeException(nameof(capa), capa, null),
    };

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
