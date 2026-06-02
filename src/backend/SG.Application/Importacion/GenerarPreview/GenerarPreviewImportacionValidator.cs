using FluentValidation;

namespace SG.Application.Importacion.GenerarPreview;

internal sealed class GenerarPreviewImportacionValidator
    : AbstractValidator<GenerarPreviewImportacionCommand>
{
    private const long MaxZipBytes = 100L * 1024 * 1024; // 100 MB

    public GenerarPreviewImportacionValidator()
    {
        RuleFor(x => x.PerfilId).NotEmpty();

        RuleFor(x => x.NombreArchivo)
            .NotEmpty()
            .MaximumLength(260);

        RuleFor(x => x.ZipStream)
            .NotNull();

        RuleFor(x => x.ZipSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxZipBytes)
            .WithMessage("El archivo ZIP no puede superar los 100 MB.");
    }
}
