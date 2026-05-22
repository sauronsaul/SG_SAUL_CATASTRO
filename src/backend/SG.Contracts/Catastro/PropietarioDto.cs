namespace SG.Contracts.Catastro;

public sealed record PropietarioDto(
    Guid Id,
    string Tipo,
    string NombreCompleto,
    string? Nombre,
    string? Apellidos,
    string? Cedula,
    string? RazonSocial,
    string? Nit,
    string? RepresentanteLegal,
    string? Email,
    string? Telefono,
    string? Direccion,
    DateTime CreadoAt);
