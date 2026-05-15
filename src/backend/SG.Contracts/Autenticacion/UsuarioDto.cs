namespace SG.Contracts.Autenticacion;

public sealed record UsuarioDto(
    Guid Id,
    string Email,
    string NombreCompleto,
    IReadOnlyList<string> Roles);
