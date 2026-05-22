using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;

namespace SG.Domain.Catastro;

public sealed class Predio : AggregateRoot
{
    private readonly List<RelacionPredioPropietario> _relaciones = [];
    private readonly List<Documento> _documentos = [];
    private readonly List<HistorialEstado> _historial = [];

    public CodigoCatastral? CodigoCatastral { get; private set; }
    public UbicacionCatastral Ubicacion { get; private set; } = null!;
    public decimal SuperficieDeclarada { get; private set; }
    public decimal? SuperficieSig { get; private set; }
    public decimal? SuperficieOficial { get; private set; }
    public Guid UsoSueloId { get; private set; }
    public EstadoPredio Estado { get; private set; }
    public GeometriaPredial? Geometria { get; private set; }

    public IReadOnlyCollection<RelacionPredioPropietario> Relaciones => _relaciones.AsReadOnly();
    public IReadOnlyCollection<Documento> Documentos => _documentos.AsReadOnly();
    public IReadOnlyCollection<HistorialEstado> Historial => _historial.AsReadOnly();

    private Predio() { }

    public static Result<Predio> Crear(
        UbicacionCatastral ubicacion,
        decimal superficieDeclarada,
        Guid usoSueloId,
        Guid creadoPor)
    {
        if (ubicacion is null)
            return Result.Failure<Predio>(PredioErrores.UbicacionRequerida);

        if (superficieDeclarada <= 0)
            return Result.Failure<Predio>(PredioErrores.SuperficieInvalida);

        if (usoSueloId == Guid.Empty)
            return Result.Failure<Predio>(PredioErrores.UsoSueloRequerido);

        var predio = new Predio
        {
            Ubicacion = ubicacion,
            SuperficieDeclarada = superficieDeclarada,
            UsoSueloId = usoSueloId,
            Estado = EstadoPredio.Borrador,
        };

        return Result.Success(predio);
    }

    // --- Máquina de estados ---

    public Result EnviarARevision(Guid usuarioId)
    {
        if (Estado != EstadoPredio.Borrador)
            return Result.Failure(PredioErrores.TransicionInvalida(Estado, EstadoPredio.EnRevision));

        CambiarEstado(EstadoPredio.EnRevision, usuarioId, null);
        return Result.Success();
    }

    public Result Validar(CodigoCatastral codigoCatastral, Guid usuarioId)
    {
        if (Estado != EstadoPredio.EnRevision)
            return Result.Failure(PredioErrores.TransicionInvalida(Estado, EstadoPredio.Validado));

        if (codigoCatastral is null)
            return Result.Failure(PredioErrores.CodigoCatastralRequerido);

        CodigoCatastral = codigoCatastral;
        CambiarEstado(EstadoPredio.Validado, usuarioId, null);
        return Result.Success();
    }

    public Result AsignarCodigoOficial(CodigoCatastral codigoOficial, Guid usuarioId)
    {
        if (codigoOficial is null)
            return Result.Failure(PredioErrores.CodigoCatastralRequerido);

        CodigoCatastral = codigoOficial;
        return Result.Success();
    }

    public Result Observar(string observaciones, Guid usuarioId)
    {
        if (Estado != EstadoPredio.EnRevision)
            return Result.Failure(PredioErrores.TransicionInvalida(Estado, EstadoPredio.Observado));

        if (string.IsNullOrWhiteSpace(observaciones))
            return Result.Failure(PredioErrores.ObservacionesRequeridas);

        CambiarEstado(EstadoPredio.Observado, usuarioId, observaciones);
        return Result.Success();
    }

    public Result RetornarBorrador(Guid usuarioId)
    {
        if (Estado != EstadoPredio.Observado)
            return Result.Failure(PredioErrores.TransicionInvalida(Estado, EstadoPredio.Borrador));

        CambiarEstado(EstadoPredio.Borrador, usuarioId, null);
        return Result.Success();
    }

    // --- Geometría ---

    public Result AsignarGeometria(GeometriaPredial geometria, Guid usuarioId)
    {
        if (geometria is null)
            return Result.Failure(GeometriaPredialErrores.PoligonoRequerido);

        Geometria = geometria;
        return Result.Success();
    }

    public Result ActualizarSuperficieSig(decimal superficie)
    {
        if (superficie <= 0)
            return Result.Failure(PredioErrores.SuperficieInvalida);

        SuperficieSig = superficie;
        return Result.Success();
    }

    public Result AsignarSuperficieOficial(decimal superficie, Guid usuarioId)
    {
        if (superficie <= 0)
            return Result.Failure(PredioErrores.SuperficieInvalida);

        SuperficieOficial = superficie;
        return Result.Success();
    }

    // --- Propietarios ---

    public Result AsignarPropietario(
        Guid propietarioId,
        TipoDerecho tipoDerecho,
        decimal porcentaje,
        DateOnly vigenteDesde,
        Guid usuarioId)
    {
        var vigentes = _relaciones.Where(r => r.EsVigente).ToList();

        if (vigentes.Any(r => r.PropietarioId == propietarioId))
            return Result.Failure(RelacionErrores.PropietarioYaVigente);

        var sumaPorcentaje = vigentes.Sum(r => r.Porcentaje);
        if (sumaPorcentaje + porcentaje > 100)
            return Result.Failure(RelacionErrores.SumaPorcentajeSuperaLimite);

        var relacionResult = RelacionPredioPropietario.Crear(
            Id, propietarioId, tipoDerecho, porcentaje, vigenteDesde, usuarioId);

        if (relacionResult.IsFailure)
            return Result.Failure(relacionResult.Error);

        _relaciones.Add(relacionResult.Value);
        return Result.Success();
    }

    public Result CerrarRelacionPropietario(Guid propietarioId, DateOnly fechaCierre, Guid usuarioId)
    {
        var relacion = _relaciones.FirstOrDefault(r => r.PropietarioId == propietarioId && r.EsVigente);
        if (relacion is null)
            return Result.Failure(RelacionErrores.YaCerrada);

        return relacion.Cerrar(fechaCierre);
    }

    // --- Documentos ---

    public Result<Documento> AgregarDocumento(
        string nombreArchivo,
        string contentType,
        long sizeBytes,
        string minioKey,
        TipoDocumento tipoDocumento,
        Guid subidoPor)
    {
        var doc = Documento.Crear(Id, nombreArchivo, contentType, sizeBytes, minioKey, tipoDocumento, subidoPor);
        _documentos.Add(doc);
        return Result.Success(doc);
    }

    public Result EliminarDocumento(Guid documentoId, Guid eliminadoPor, string motivo)
    {
        var doc = _documentos.FirstOrDefault(d => d.Id == documentoId);
        if (doc is null)
            return Result.Failure(DocumentoErrores.NoEncontrado);

        return doc.Eliminar(eliminadoPor, motivo);
    }

    // --- Privados ---

    private void CambiarEstado(EstadoPredio nuevo, Guid usuarioId, string? observaciones)
    {
        var anterior = Estado;
        Estado = nuevo;
        _historial.Add(HistorialEstado.Registrar(Id, anterior, nuevo, usuarioId, observaciones));
    }
}

public static class PredioErrores
{
    public static readonly DomainError CodigoCatastralRequerido = new("Predio.CodigoCatastralRequerido", "El código catastral es requerido.");
    public static readonly DomainError UbicacionRequerida = new("Predio.UbicacionRequerida", "La ubicación catastral es requerida.");
    public static readonly DomainError SuperficieInvalida = new("Predio.SuperficieInvalida", "La superficie debe ser mayor a cero.");
    public static readonly DomainError UsoSueloRequerido = new("Predio.UsoSueloRequerido", "El uso de suelo es requerido.");
    public static readonly DomainError ObservacionesRequeridas = new("Predio.ObservacionesRequeridas", "Las observaciones son requeridas para observar un predio.");
    public static readonly DomainError NoEncontrado = new("Predio.NoEncontrado", "El predio no fue encontrado.");
    public static readonly DomainError CodigoCatastralDuplicado = new("Predio.CodigoCatastralDuplicado", "Ya existe un predio con ese código catastral.");

    public static DomainError TransicionInvalida(EstadoPredio actual, EstadoPredio destino) =>
        new("Predio.TransicionInvalida",
            $"No es posible transicionar de '{actual}' a '{destino}'.");
}
