using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Serenity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Features.Auth.EmisorSesion>();
        services.AddScoped<Features.Contenido.CargadorContenido>();
        services.AddScoped<Features.Proyectos.ProyectoEnsamblador>();
        services.AddScoped<Features.Anuncios.AnuncioSyncService>();
        services.AddScoped<Features.Reservas.AlojamientoEnsamblador>();
        services.AddScoped<Features.Reservas.CalendarioAlojamientos>();
        services.AddScoped<Features.Reservas.ProcesadorPagos>();
        services.AddScoped<Features.Notificaciones.Notificador>();

        return services;
    }
}
