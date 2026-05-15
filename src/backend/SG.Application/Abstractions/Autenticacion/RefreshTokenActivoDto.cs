namespace SG.Application.Abstractions.Autenticacion;

public sealed record RefreshTokenActivoDto(
    Guid Id,
    Guid UsuarioId,
    bool IsActive,
    bool EstaRevocado,
    bool EstaExpirado,
    string? ReplacedByToken);
