using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed record CrearVersionImportacionCommand(
    string NombreArchivo,
    Stream PaqueteStream,
    long TamanoBytes) : IRequest<Result<CrearVersionImportacionDto>>;
