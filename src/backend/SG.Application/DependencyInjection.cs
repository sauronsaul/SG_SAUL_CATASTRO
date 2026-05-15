using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace SG.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
