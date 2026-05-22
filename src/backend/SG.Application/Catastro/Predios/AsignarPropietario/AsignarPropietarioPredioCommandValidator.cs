using FluentValidation;

namespace SG.Application.Catastro.Predios.AsignarPropietario;

internal sealed class AsignarPropietarioPredioCommandValidator
    : AbstractValidator<AsignarPropietarioPredioCommand>
{
    public AsignarPropietarioPredioCommandValidator()
    {
        RuleFor(x => x.PredioId).NotEqual(Guid.Empty);
        RuleFor(x => x.PropietarioId).NotEqual(Guid.Empty);
        RuleFor(x => x.Porcentaje).InclusiveBetween(0.01m, 100m);
    }
}
