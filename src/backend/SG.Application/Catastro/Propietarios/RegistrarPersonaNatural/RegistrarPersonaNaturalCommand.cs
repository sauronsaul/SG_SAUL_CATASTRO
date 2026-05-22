using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.RegistrarPersonaNatural;

public sealed record RegistrarPersonaNaturalCommand(
    string Nombre,
    string Apellidos,
    string Cedula,
    string? Email,
    string? Telefono,
    string? Direccion) : IRequest<Result<Guid>>;
