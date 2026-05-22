using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.RegistrarPersonaJuridica;

public sealed record RegistrarPersonaJuridicaCommand(
    string RazonSocial,
    string Nit,
    string? RepresentanteLegal,
    string? Email,
    string? Telefono,
    string? Direccion) : IRequest<Result<Guid>>;
