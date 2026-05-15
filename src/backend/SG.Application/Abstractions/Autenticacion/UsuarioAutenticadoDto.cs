namespace SG.Application.Abstractions.Autenticacion;

public sealed record UsuarioAutenticadoDto(
    Guid Id,
    string Email,
    string NombreCompleto,
    bool EstaBloquado);
