using FluentValidation;

namespace SG.Application.Catastro.Predios.CambioEstado;

internal sealed class ObservarPredioCommandValidator : AbstractValidator<ObservarPredioCommand>
{
    public ObservarPredioCommandValidator()
    {
        RuleFor(x => x.PredioId).NotEqual(Guid.Empty);
        RuleFor(x => x.Observaciones).NotEmpty().MaximumLength(1000);
    }
}
