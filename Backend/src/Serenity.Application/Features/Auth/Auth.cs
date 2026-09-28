using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Domain.Entities;

namespace Serenity.Application.Features.Auth;

public sealed record UsuarioDto(long Id, string Nombre, string Correo, string Rol);

public sealed record AuthTokensDto(string AccessToken, string RefreshToken, int ExpiresIn);

public sealed record LoginResponseDto(UsuarioDto User, AuthTokensDto Tokens);

/// <summary>Emite y persiste el par access/refresh. Compartido por login y refresh.</summary>
public sealed class EmisorSesion(
    IJwtTokenService jwt,
    IRefreshTokenRepository refreshTokens)
{
    public async Task<AuthTokensDto> EmitirAsync(Usuario usuario, CancellationToken ct)
    {
        var access = jwt.GenerarAccessToken(usuario);
        var refresh = jwt.GenerarRefreshToken();

        await refreshTokens.GuardarAsync(usuario.Id, TokenHasher.Hash(refresh.Token), refresh.ExpiraEnUtc, ct);

        return new AuthTokensDto(access.Token, refresh.Token, access.ExpiraEnSegundos);
    }

    public static UsuarioDto ADto(Usuario u) => new(u.Id, u.Nombre, u.Correo, u.Rol);
}

// ─── Login ────────────────────────────────────────────────────────────────────

public sealed record LoginCommand(string Correo, string Password) : IRequest<Result<LoginResponseDto>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Correo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginCommandHandler(
    IUsuarioRepository usuarios,
    IPasswordHasher hasher,
    EmisorSesion emisor,
    TimeProvider reloj) : IRequestHandler<LoginCommand, Result<LoginResponseDto>>
{
    private const string Generico = "Correo o contraseña incorrectos.";

    // Hash de un valor cualquiera: se verifica contra él cuando el correo no existe
    // para que el tiempo de respuesta no delate qué correos están registrados.
    private static string? _hashSenuelo;

    public async Task<Result<LoginResponseDto>> Handle(LoginCommand request, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var usuario = await usuarios.ObtenerPorCorreoAsync(request.Correo.Trim().ToLowerInvariant(), ct);

        if (usuario is null || !usuario.Activo)
        {
            _hashSenuelo ??= hasher.Hash("senuelo-no-es-una-clave");
            hasher.Verify(request.Password, _hashSenuelo);
            return Result.Failure<LoginResponseDto>(Generico, ErrorTipo.NoAutorizado);
        }

        if (usuario.EstaBloqueado(ahora))
        {
            return Bloqueado(usuario, ahora);
        }

        if (!hasher.Verify(request.Password, usuario.PasswordHash))
        {
            var bloqueado = usuario.RegistrarLoginFallido(ahora);
            await usuarios.GuardarBloqueoAsync(usuario, ct);

            return bloqueado
                ? Bloqueado(usuario, ahora)
                : Result.Failure<LoginResponseDto>(Generico, ErrorTipo.NoAutorizado);
        }

        usuario.RegistrarLoginExitoso();
        await usuarios.GuardarBloqueoAsync(usuario, ct);

        var tokens = await emisor.EmitirAsync(usuario, ct);
        return Result.Success(new LoginResponseDto(EmisorSesion.ADto(usuario), tokens));
    }

    private static Result<LoginResponseDto> Bloqueado(Usuario usuario, DateTime ahora)
        => Result.Failure<LoginResponseDto>(
            $"Demasiados intentos fallidos. Intenta de nuevo en {Math.Max(1, usuario.MinutosDeBloqueoRestantes(ahora))} minuto(s).",
            ErrorTipo.Bloqueado);
}

// ─── Refresh (rotación) ───────────────────────────────────────────────────────

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(500);
}

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUsuarioRepository usuarios,
    EmisorSesion emisor) : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var usuarioId = await refreshTokens.ConsumirAsync(TokenHasher.Hash(request.RefreshToken), ct);
        if (usuarioId is null)
        {
            return Result.Failure<AuthTokensDto>("Sesión inválida o vencida.", ErrorTipo.NoAutorizado);
        }

        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId.Value, ct);
        if (usuario is null || !usuario.Activo)
        {
            return Result.Failure<AuthTokensDto>("Sesión inválida o vencida.", ErrorTipo.NoAutorizado);
        }

        return Result.Success(await emisor.EmitirAsync(usuario, ct));
    }
}

// ─── Logout ───────────────────────────────────────────────────────────────────

public sealed record LogoutCommand(long UsuarioId) : IRequest<Result>;

public sealed class LogoutCommandHandler(IRefreshTokenRepository refreshTokens) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        await refreshTokens.RevocarTodosAsync(request.UsuarioId, ct);
        return Result.Success();
    }
}

// ─── Me ───────────────────────────────────────────────────────────────────────

public sealed record ObtenerUsuarioActualQuery(long UsuarioId) : IRequest<Result<UsuarioDto>>;

public sealed class ObtenerUsuarioActualQueryHandler(IUsuarioRepository usuarios)
    : IRequestHandler<ObtenerUsuarioActualQuery, Result<UsuarioDto>>
{
    public async Task<Result<UsuarioDto>> Handle(ObtenerUsuarioActualQuery request, CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(request.UsuarioId, ct);
        return usuario is { Activo: true }
            ? Result.Success(EmisorSesion.ADto(usuario))
            : Result.Failure<UsuarioDto>("Usuario no encontrado.", ErrorTipo.NoAutorizado);
    }
}

// ─── Cambiar contraseña ───────────────────────────────────────────────────────

public sealed record CambiarPasswordCommand(long UsuarioId, string PasswordActual, string PasswordNueva) : IRequest<Result>;

public sealed class CambiarPasswordCommandValidator : AbstractValidator<CambiarPasswordCommand>
{
    public CambiarPasswordCommandValidator()
    {
        RuleFor(x => x.PasswordActual).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PasswordNueva).Password();
        RuleFor(x => x).Must(x => x.PasswordActual != x.PasswordNueva)
            .WithName("PasswordNueva").WithMessage("La nueva contraseña debe ser distinta de la actual.");
    }
}

public sealed class CambiarPasswordCommandHandler(
    IUsuarioRepository usuarios,
    IPasswordHasher hasher,
    IRefreshTokenRepository refreshTokens) : IRequestHandler<CambiarPasswordCommand, Result>
{
    public async Task<Result> Handle(CambiarPasswordCommand request, CancellationToken ct)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(request.UsuarioId, ct);
        if (usuario is null || !usuario.Activo)
        {
            return Result.Failure("Usuario no encontrado.", ErrorTipo.NoAutorizado);
        }

        if (!hasher.Verify(request.PasswordActual, usuario.PasswordHash))
        {
            return Result.Failure("La contraseña actual es incorrecta.");
        }

        await usuarios.ActualizarPasswordAsync(usuario.Id, hasher.Hash(request.PasswordNueva), ct);
        await refreshTokens.RevocarTodosAsync(usuario.Id, ct);

        return Result.Success();
    }
}
