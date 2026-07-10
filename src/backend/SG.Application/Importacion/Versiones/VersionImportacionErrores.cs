using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public static class VersionImportacionErrores
{
    public static readonly DomainError PaqueteInvalido =
        new("VersionImportacion.PaqueteInvalido", "El paquete ZIP de las siete capas es inválido.");
    public static readonly DomainError NoEncontrada =
        new("VersionImportacion.NoEncontrada", "La versión de dataset no fue encontrada.");
}
