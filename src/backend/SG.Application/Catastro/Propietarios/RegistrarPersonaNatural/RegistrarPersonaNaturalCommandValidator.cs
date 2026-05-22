using FluentValidation;

namespace SG.Application.Catastro.Propietarios.RegistrarPersonaNatural;

internal sealed class RegistrarPersonaNaturalCommandValidator : AbstractValidator<RegistrarPersonaNaturalCommand>
{
    public RegistrarPersonaNaturalCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Apellidos).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Cedula).NotEmpty().MaximumLength(15);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Telefono).MaximumLength(20);
        RuleFor(x => x.Direccion).MaximumLength(300);
    }
}
