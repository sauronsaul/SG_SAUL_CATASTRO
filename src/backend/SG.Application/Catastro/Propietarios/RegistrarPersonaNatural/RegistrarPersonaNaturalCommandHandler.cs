using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.RegistrarPersonaNatural;

public sealed class RegistrarPersonaNaturalCommandHandler(
    IPropietarioRepositorio propietarios)
    : IRequestHandler<RegistrarPersonaNaturalCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RegistrarPersonaNaturalCommand request,
        CancellationToken cancellationToken)
    {
        var cedulaNorm = request.Cedula.Trim().ToUpperInvariant();

        if (await propietarios.ExisteCedulaAsync(cedulaNorm, cancellationToken))
            return Result.Failure<Guid>(PropietarioErrores.CedulaDuplicada);

        var result = Propietario.CrearPersonaNatural(
            request.Nombre,
            request.Apellidos,
            cedulaNorm,
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
