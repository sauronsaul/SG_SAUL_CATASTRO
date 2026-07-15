using FluentValidation;

namespace SG.Application.Catastro.Predios.BuscarPorTriplete;

internal sealed class BuscarFichaPredioQueryValidator : AbstractValidator<BuscarFichaPredioQuery>
{
    public BuscarFichaPredioQueryValidator()
    {
        RuleFor(x => x.Distrito)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El distrito debe ser mayor o igual a 1.");
        RuleFor(x => x.Manzana)
            .GreaterThanOrEqualTo(1)
            .WithMessage("La manzana debe ser mayor o igual a 1.");
        RuleFor(x => x.Predio)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El predio debe ser mayor o igual a 1.");
    }
}
