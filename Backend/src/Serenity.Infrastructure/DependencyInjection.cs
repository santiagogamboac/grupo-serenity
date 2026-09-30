using Amazon.S3;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serenity.Application.Interfaces;
using Serenity.Infrastructure.Persistence;
using Serenity.Infrastructure.Repositories;
using Serenity.Infrastructure.Services;
using Serenity.Infrastructure.Storage;

namespace Serenity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default no configurada.");

        // snake_case de Postgres ↔ PascalCase de los DTOs, sin alias en cada consulta.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        services.AddSingleton(new DbConnectionFactory(connectionString));
        services.AddSingleton<SqlMigrator>();

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<ISeccionRepository, SeccionRepository>();
        services.AddScoped<IProyectoRepository, ProyectoRepository>();
        services.AddScoped<IResenaRepository, ResenaRepository>();
        services.AddScoped<IValorRepository, ValorRepository>();
        services.AddScoped<ISolicitudRepository, SolicitudRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IEstadoAppRepository, EstadoAppRepository>();
        services.AddScoped<IAnuncioRepository, AnuncioRepository>();
        services.AddSingleton<IIcalClient, HttpIcalClient>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.Configure<OpcionesAlmacenamiento>(configuration.GetSection(OpcionesAlmacenamiento.Seccion));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<OpcionesAlmacenamiento>>().Value;
            var config = new AmazonS3Config { ForcePathStyle = o.ForcePathStyle, AuthenticationRegion = o.Region };

            if (!string.IsNullOrWhiteSpace(o.Endpoint))
            {
                config.ServiceURL = o.Endpoint;
                config.UseHttp = o.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.Region);
            }

            return new AmazonS3Client(o.AccessKey, o.SecretKey, config);
        });
        services.AddSingleton<IAlmacenamientoObjetos, S3AlmacenamientoObjetos>();

        return services;
    }
}
