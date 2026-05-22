using FluentValidation;

namespace SG.Application.Catastro.Predios.SubirDocumento;

internal sealed class SubirDocumentoPredioCommandValidator : AbstractValidator<SubirDocumentoPredioCommand>
{
    private const long MaxBytes = 20 * 1024 * 1024; // 20 MB

    public SubirDocumentoPredioCommandValidator()
    {
        RuleFor(x => x.PredioId).NotEqual(Guid.Empty);
        RuleFor(x => x.NombreArchivo).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeBytes).GreaterThan(0).LessThanOrEqualTo(MaxBytes);
        RuleFor(x => x.Contenido).NotNull();
    }
}
