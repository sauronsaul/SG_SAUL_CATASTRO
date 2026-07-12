using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SG.Application.Abstractions;
using SG.Application.Importacion;
using SG.Application.Importacion.Versiones;

namespace SG.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddSingleton<IMapeadorImportacion, MapeadorImportacion>();
        services.AddSingleton<IColaCargaVersionada, ColaCargaVersionada>();
        services.AddScoped<PipelineShapefileService>();

        return services;
    }
}
