using SG.Domain.Common;

namespace SG.Domain.Usuarios;

public static class UsuarioErrores
{
    public static readonly DomainError EmailInvalido =
        new("Usuario.EmailInvalido", "El email no tiene un formato válido.");

    public static readonly DomainError EmailRequerido =
        new("Usuario.EmailRequerido", "El email es obligatorio.");

    public static readonly DomainError NombreVacio =
        new("Usuario.NombreVacio", "El nombre completo es obligatorio.");

    public static readonly DomainError EmailDuplicado =
        new("Usuario.EmailDuplicado", "Ya existe un usuario registrado con ese email.");
}
