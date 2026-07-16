using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SG.Application.Abstractions.Catastro;
using SG.Contracts.Catastro;
using SG.Infrastructure.GIS;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Catastro;

internal sealed class ConsultaPredioVersionado(
    ApplicationDbContext db,
    IOptions<TilesSettings> settings) : IConsultaPredioVersionado
{
    private const string Sql = """
        WITH version_activa AS MATERIALIZED (
            SELECT id, numero_version, municipio_codigo
            FROM dominio.dataset_versiones
            WHERE municipio_codigo = @municipio
              AND estado = 'Activa'
            LIMIT 1
        )
        SELECT p.id AS predio_id,
               v.id AS dataset_version_id,
               v.numero_version,
               v.municipio_codigo,
               c.fila_origen,
               c.cod_uv,
               c.cod_man,
               c.cod_pred,
               p.codigo_catastral,
               c.codigo_geografico,
               p.estado,
               c.superficie AS superficie_declarada_m2,
               round(ST_Area(c.geometria)::numeric, 4) AS superficie_grafica_m2,
               p.superficie_oficial AS superficie_oficial_m2,
               COALESCE(
                   NULLIF(BTRIM(p.propietario_referencia), ''),
                   NULLIF(BTRIM(c.nombre_propietario_origen), '')) AS propietario_referencia,
               NULLIF(BTRIM(c.tipo_inmueble), '') AS tipo_inmueble,
               NULLIF(BTRIM(c.nombre_via), '') AS nombre_via,
               NULLIF(BTRIM(c.direccion_barrio), '') AS barrio,
               NULLIF(BTRIM(c.direccion_urbana), '') AS direccion,
               NULLIF(BTRIM(c.uso_terreno), '') AS uso_terreno,
               NULLIF(BTRIM(c.topografia_terreno), '') AS topografia_terreno,
               NULLIF(BTRIM(c.servicio_agua), '') AS servicio_agua,
               NULLIF(BTRIM(c.servicio_luz), '') AS servicio_luz,
               NULLIF(BTRIM(c.servicio_alcantarillado), '') AS servicio_alcantarillado,
               NULLIF(BTRIM(c.servicio_telefonia), '') AS servicio_telefonia,
               c.geometria AS geometria_planar,
               ST_XMin(ST_Envelope(ST_Transform(c.geometria, 4326))) AS oeste,
               ST_YMin(ST_Envelope(ST_Transform(c.geometria, 4326))) AS sur,
               ST_XMax(ST_Envelope(ST_Transform(c.geometria, 4326))) AS este,
               ST_YMax(ST_Envelope(ST_Transform(c.geometria, 4326))) AS norte
        FROM version_activa v
        JOIN dominio.capa_parcelas c ON c.dataset_version_id = v.id
        JOIN dominio.predios p
          ON p.municipio_codigo = v.municipio_codigo
         AND p.cod_uv = c.cod_uv
         AND p.cod_man = c.cod_man
         AND p.cod_pred = c.cod_pred
         AND p.presente_en_version_activa
         AND NOT p.is_deleted
        WHERE c.cod_uv = @distrito
          AND c.cod_man = @manzana
          AND c.cod_pred = @predio;
        """;

    public async Task<FichaPredioDto?> BuscarAsync(
        int distrito,
        int manzana,
        int predio,
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
            command.CommandText = Sql;
            command.CommandTimeout = 30;
            AgregarParametro(command, "municipio", municipio, DbType.String);
            AgregarParametro(command, "distrito", distrito, DbType.Int32);
            AgregarParametro(command, "manzana", manzana, DbType.Int32);
            AgregarParametro(command, "predio", predio, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new FichaPredioDto(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                ObtenerStringNullable(reader, 8),
                ObtenerStringNullable(reader, 9),
                reader.GetString(10),
                reader.GetDecimal(11),
                reader.GetDecimal(12),
                ObtenerDecimalNullable(reader, 13),
                ObtenerStringNullable(reader, 14),
                ObtenerStringNullable(reader, 15),
                ObtenerStringNullable(reader, 16),
                ObtenerStringNullable(reader, 17),
                ObtenerStringNullable(reader, 18),
                ObtenerStringNullable(reader, 19),
                ObtenerStringNullable(reader, 20),
                ObtenerStringNullable(reader, 21),
                ObtenerStringNullable(reader, 22),
                ObtenerStringNullable(reader, 23),
                ObtenerStringNullable(reader, 24),
                CrearGeometriaPlanar(reader.GetFieldValue<Polygon>(25)),
                new LimitesPredioDto(
                    reader.GetDouble(26),
                    reader.GetDouble(27),
                    reader.GetDouble(28),
                    reader.GetDouble(29)));
        }
        finally
        {
            if (cerrarConexion)
                await connection.CloseAsync();
        }
    }

    private static string? ObtenerStringNullable(System.Data.Common.DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static decimal? ObtenerDecimalNullable(System.Data.Common.DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    private static GeometriaPlanarDto CrearGeometriaPlanar(Polygon poligono)
    {
        var anillos = new double[poligono.NumInteriorRings + 1][][];
        anillos[0] = ConvertirAnillo(poligono.ExteriorRing);
        for (var indice = 0; indice < poligono.NumInteriorRings; indice++)
            anillos[indice + 1] = ConvertirAnillo(poligono.GetInteriorRingN(indice));

        return new GeometriaPlanarDto(poligono.SRID, "Polygon", anillos);
    }

    private static double[][] ConvertirAnillo(LineString anillo) =>
        anillo.Coordinates
            .Select(coordenada => new[] { coordenada.X, coordenada.Y })
            .ToArray();

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
