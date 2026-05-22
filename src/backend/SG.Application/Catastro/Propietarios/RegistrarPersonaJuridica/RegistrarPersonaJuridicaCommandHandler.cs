using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.RegistrarPersonaJuridica;

public sealed class RegistrarPersonaJuridicaCommandHandler(
    IPropietarioRepositorio propietarios)
    : IRequestHandler<RegistrarPersonaJuridicaCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RegistrarPersonaJuridicaCommand request,
        CancellationToken cancellationToken)
    {
        if (await propietarios.ExisteNitAsync(request.Nit.Trim(), cancellationToken))
            return Result.Failure<Guid>(PropietarioErrores.NitDuplicado);

        var result = Propietario.CrearPersonaJuridica(
            request.RazonSocial,
            request.Nit,
            request.RepresentanteLegal,
            request.Email,
            request.Telefono,
            request.Direccion);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        propietarios.Agregar(result.Value);
        await propietarios.GuardarCambiosAsync(cancellationToken);

        return Result.Success(result.Value.Id);
    }
}
