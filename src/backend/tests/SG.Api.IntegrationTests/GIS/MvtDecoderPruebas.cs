using System.Text;

namespace SG.Api.IntegrationTests.GIS;

internal static class MvtDecoderPruebas
{
    internal sealed record Capa(
        string Nombre,
        IReadOnlyList<ulong> FeatureIds,
        IReadOnlyList<string> Claves);

    public static IReadOnlyList<Capa> Decodificar(byte[] tile)
    {
        var capas = new List<Capa>();
        var offset = 0;
        while (offset < tile.Length)
        {
            var clave = LeerVarint(tile, ref offset);
            var campo = (int)(clave >> 3);
            var wireType = (int)(clave & 7);
            if (campo == 3 && wireType == 2)
            {
                var bloque = LeerBloque(tile, ref offset);
                capas.Add(LeerCapa(bloque));
            }
            else
            {
                Omitir(tile, ref offset, wireType);
            }
        }

        return capas;
    }

    private static Capa LeerCapa(ReadOnlySpan<byte> datos)
    {
        var nombre = string.Empty;
        var ids = new List<ulong>();
        var claves = new List<string>();
        var offset = 0;
        while (offset < datos.Length)
        {
            var clave = LeerVarint(datos, ref offset);
            var campo = (int)(clave >> 3);
            var wireType = (int)(clave & 7);
            if (campo == 1 && wireType == 2)
                nombre = Encoding.UTF8.GetString(LeerBloque(datos, ref offset));
            else if (campo == 2 && wireType == 2)
                ids.Add(LeerFeatureId(LeerBloque(datos, ref offset)));
            else if (campo == 3 && wireType == 2)
                claves.Add(Encoding.UTF8.GetString(LeerBloque(datos, ref offset)));
            else
                Omitir(datos, ref offset, wireType);
        }

        return new Capa(nombre, ids, claves);
    }

    private static ulong LeerFeatureId(ReadOnlySpan<byte> datos)
    {
        var offset = 0;
        while (offset < datos.Length)
        {
            var clave = LeerVarint(datos, ref offset);
            var campo = (int)(clave >> 3);
            var wireType = (int)(clave & 7);
            if (campo == 1 && wireType == 0)
                return LeerVarint(datos, ref offset);

            Omitir(datos, ref offset, wireType);
        }

        throw new InvalidDataException("Feature MVT sin id.");
    }

    private static ReadOnlySpan<byte> LeerBloque(ReadOnlySpan<byte> datos, ref int offset)
    {
        var longitud = checked((int)LeerVarint(datos, ref offset));
        if (longitud < 0 || offset + longitud > datos.Length)
            throw new InvalidDataException("Bloque protobuf truncado.");

        var bloque = datos.Slice(offset, longitud);
        offset += longitud;
        return bloque;
    }

    private static ulong LeerVarint(ReadOnlySpan<byte> datos, ref int offset)
    {
        ulong valor = 0;
        for (var desplazamiento = 0; desplazamiento < 64; desplazamiento += 7)
        {
            if (offset >= datos.Length)
                throw new InvalidDataException("Varint protobuf truncado.");

            var actual = datos[offset++];
            valor |= (ulong)(actual & 0x7f) << desplazamiento;
            if ((actual & 0x80) == 0)
                return valor;
        }

        throw new InvalidDataException("Varint protobuf invalido.");
    }

    private static void Omitir(ReadOnlySpan<byte> datos, ref int offset, int wireType)
    {
        switch (wireType)
        {
            case 0:
                LeerVarint(datos, ref offset);
                break;
            case 1:
                offset += 8;
                break;
            case 2:
                var longitud = checked((int)LeerVarint(datos, ref offset));
                offset += longitud;
                break;
            case 5:
                offset += 4;
                break;
            default:
                throw new InvalidDataException($"Wire type protobuf no soportado: {wireType}.");
        }

        if (offset > datos.Length)
            throw new InvalidDataException("Campo protobuf truncado.");
    }
}
