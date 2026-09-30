using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Serenity.Api.Middlewares;
using Serenity.Api.Seeding;
using Serenity.Application;
using Serenity.Application.Common;
using Serenity.Infrastructure;
using Serenity.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    Log.Information("Iniciando Serenity.Api...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<DatabaseSeeder>();
    builder.Services.AddSingleton(builder.Configuration.GetSection("Airbnb").Get<Serenity.Application.Features.Anuncios.OpcionesAirbnb>()
                                  ?? new Serenity.Application.Features.Anuncios.OpcionesAirbnb());
    builder.Services.AddHostedService<Serenity.Api.Workers.AirbnbSyncWorker>();

    // ─── JWT ──────────────────────────────────────────────────────────────────
    var jwt = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwt["Key"];
    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:Key debe estar configurada y tener al menos 32 caracteres.");
    }

    if (builder.Environment.IsProduction() && jwtKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Jwt:Key sigue con el valor de ejemplo (CHANGE_ME). Define un secreto real en producción.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Los claims se conservan con su nombre corto ("sub", "email"), sin el mapeo a URIs de WS-*.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = "name"
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("AdminOnly", p => p.RequireRole(Serenity.Domain.Roles.Admin));

    builder.Services.AddControllers();

    // ─── Subida de imágenes: multipart hasta 10 MB (+ margen del sobre multipart) ─
    builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = ReglasMedia.MaxBytes + 1024 * 1024);
    builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = ReglasMedia.MaxBytes + 1024 * 1024);

    // ─── CORS (solo relevante si el front se sirve desde otro origen; con nginx no hace falta) ─
    var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
        p.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

    // ─── Rate limiting por IP ─────────────────────────────────────────────────
    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        o.AddPolicy("solicitudes", ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

        // Freno al login por IP (además del bloqueo por cuenta).
        o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    });

    // nginx (u otro proxy) termina la conexión: la IP real llega en X-Forwarded-For.
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                             | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });

    builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");

    var app = builder.Build();

    // ─── Esquema + datos iniciales antes de aceptar tráfico ───────────────────
    // El seed corre dentro del lock de migración: con varias réplicas, solo una siembra.
    await app.Services.GetRequiredService<SqlMigrator>().MigrarAsync(async ct =>
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().EjecutarAsync(ct);
    });

    app.UseForwardedHeaders();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors("Frontend");
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La aplicación terminó de forma inesperada.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
