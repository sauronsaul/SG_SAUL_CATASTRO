using FluentValidation;

namespace SG.Application.Catastro.Predios.EliminarDocumento;

internal sealed class EliminarDocumentoPredioCommandValidator : AbstractValidator<EliminarDocumentoPredioCommand>
{
    public EliminarDocumentoPredioCommandValidator()
    {
        RuleFor(x => x.PredioId).NotEqual(Guid.Empty);
        RuleFor(x => x.DocumentoId).NotEqual(Guid.Empty);
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }
}
