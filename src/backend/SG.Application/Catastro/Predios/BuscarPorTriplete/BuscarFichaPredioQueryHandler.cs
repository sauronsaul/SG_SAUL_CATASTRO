using FluentValidation;
using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Contracts.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.BuscarPorTriplete;

public sealed class BuscarFichaPredioQueryHandler(
    IConsultaPredioVersionado consulta,
    IValidator<BuscarFichaPredioQuery> validator)
    : IRequestHandler<BuscarFichaPredioQuery, Result<FichaPredioDto>>
{
    public async Task<Result<FichaPredioDto>> Handle(
        BuscarFichaPredioQuery request,
        CancellationToken cancellationToken)
    {
        var validacion = await validator.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
            return Result.Failure<FichaPredioDto>(FichaPredioErrores.CriterioInvalido);

        var ficha = await consulta.BuscarAsync(
            request.MunicipioCodigo,
            request.Distrito,
            request.Manzana,
            request.Predio,
            cancellationToken);

        return ficha is null
            ? Result.Failure<FichaPredioDto>(FichaPredioErrores.NoEncontrado)
            : Result.Success(ficha);
    }
}

public static class FichaPredioErrores
{
    public static readonly DomainError CriterioInvalido = new(
        "FichaPredio.CriterioInvalido",
        "El municipio debe ser un código INE válido y distrito, manzana y predio deben ser mayores o iguales a 1.");

    public static readonly DomainError NoEncontrado = new(
        "FichaPredio.NoEncontrado",
        "No se encontro un predio activo con el distrito, manzana y predio indicados.");
}
