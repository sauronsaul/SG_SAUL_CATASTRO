using FluentValidation;

namespace SG.Application.Catastro.Propietarios.RegistrarPersonaJuridica;

internal sealed class RegistrarPersonaJuridicaCommandValidator : AbstractValidator<RegistrarPersonaJuridicaCommand>
{
    public RegistrarPersonaJuridicaCommandValidator()
    {
        RuleFor(x => x.RazonSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Nit).NotEmpty().MaximumLength(13).Matches(@"^\d+$").WithMessage("El NIT debe ser numérico.");
        RuleFor(x => x.RepresentanteLegal).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Telefono).MaximumLength(20);
        RuleFor(x => x.Direccion).MaximumLength(300);
    }
}
