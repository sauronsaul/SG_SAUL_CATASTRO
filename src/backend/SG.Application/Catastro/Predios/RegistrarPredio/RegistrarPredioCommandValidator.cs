using FluentValidation;

namespace SG.Application.Catastro.Predios.RegistrarPredio;

internal sealed class RegistrarPredioCommandValidator : AbstractValidator<RegistrarPredioCommand>
{
    public RegistrarPredioCommandValidator()
    {
        RuleFor(x => x.UbicacionZona).NotEmpty().MaximumLength(50);
        RuleFor(x => x.UbicacionManzana).NotEmpty().MaximumLength(20);
        RuleFor(x => x.UbicacionLote).NotEmpty().MaximumLength(20);
        RuleFor(x => x.UbicacionBarrio).MaximumLength(100);
        RuleFor(x => x.UbicacionDireccion).MaximumLength(300);
        RuleFor(x => x.UbicacionReferencia).MaximumLength(300);
        RuleFor(x => x.SuperficieDeclarada).GreaterThan(0);
        RuleFor(x => x.UsoSueloId).NotEqual(Guid.Empty);
    }
}
