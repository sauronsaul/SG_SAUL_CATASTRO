using SG.Domain.Catastro.Enums;
using SG.Domain.Common;

namespace SG.Domain.Catastro;

public sealed class RelacionPredioPropietario
{
    public Guid Id { get; private set; }
    public Guid PredioId { get; private set; }
    public Guid PropietarioId { get; private set; }
    public TipoDerecho TipoDerecho { get; private set; }
    public decimal Porcentaje { get; private set; }
    public DateOnly VigenteDesde { get; private set; }
    public DateOnly? VigenteHasta { get; private set; }
    public Guid CreadoPor { get; private set; }
    public DateTime CreadoAt { get; private set; }

    public bool EsVigente => VigenteHasta is null;

    private RelacionPredioPropietario() { }

    internal static Result<RelacionPredioPropietario> Crear(
        Guid predioId,
        Guid propietarioId,
        TipoDerecho tipoDerecho,
        decimal porcentaje,
        DateOnly vigenteDesde,
        Guid creadoPor)
    {
        if (porcentaje <= 0 || porcentaje > 100)
            return Result.Failure<RelacionPredioPropietario>(RelacionErrores.PorcentajeInvalido);

        return Result.Success(new RelacionPredioPropietario
        {
            Id = Guid.NewGuid(),
            PredioId = predioId,
            PropietarioId = propietarioId,
            TipoDerecho = tipoDerecho,
            Porcentaje = porcentaje,
            VigenteDesde = vigenteDesde,
            VigenteHasta = null,
            CreadoPor = creadoPor,
            CreadoAt = DateTime.UtcNow,
        });
    }

    internal Result Cerrar(DateOnly fechaCierre)
    {
        if (!EsVigente)
            return Result.Failure(RelacionErrores.YaCerrada);

        if (fechaCierre < VigenteDesde)
            return Result.Failure(RelacionErrores.FechaCierreAnteriorAInicio);

        VigenteHasta = fechaCierre;
        return Result.Success();
    }
}

public static class RelacionErrores
{
    public static readonly DomainError PorcentajeInvalido = new("Relacion.PorcentajeInvalido", "El porcentaje debe ser mayor a 0 y no mayor a 100.");
    public static readonly DomainError YaCerrada = new("Relacion.YaCerrada", "La relación predio-propietario ya está cerrada.");
    public static readonly DomainError FechaCierreAnteriorAInicio = new("Relacion.FechaCierreAnteriorAInicio", "La fecha de cierre no puede ser anterior a la fecha de inicio de la vigencia.");
    public static readonly DomainError SumaPorcentajeSuperaLimite = new("Relacion.SumaPorcentajeSuperaLimite", "La suma de porcentajes de propietarios vigentes no puede superar el 100%.");
    public static readonly DomainError PropietarioYaVigente = new("Relacion.PropietarioYaVigente", "El propietario ya tiene una relación vigente con este predio.");
}
