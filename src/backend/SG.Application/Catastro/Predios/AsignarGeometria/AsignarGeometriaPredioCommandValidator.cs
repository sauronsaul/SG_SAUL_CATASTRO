using FluentValidation;

namespace SG.Application.Catastro.Predios.AsignarGeometria;

internal sealed class AsignarGeometriaPredioCommandValidator : AbstractValidator<AsignarGeometriaPredioCommand>
{
    public AsignarGeometriaPredioCommandValidator()
    {
        RuleFor(x => x.PredioId).NotEqual(Guid.Empty);
        RuleFor(x => x.Geometria).NotEmpty();
        RuleFor(x => x.Formato)
            .NotEmpty()
            .Must(f => f == "WKT" || f == "GeoJSON")
            .WithMessage("El formato debe ser 'WKT' o 'GeoJSON'.");
    }
}
