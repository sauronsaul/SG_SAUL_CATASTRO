using FluentValidation;

namespace SG.Application.Importacion.Confirmar;

internal sealed class ConfirmarImportacionValidator
    : AbstractValidator<ConfirmarImportacionCommand>
{
    public ConfirmarImportacionValidator()
    {
        RuleFor(x => x.ImportacionId).NotEmpty();
    }
}
