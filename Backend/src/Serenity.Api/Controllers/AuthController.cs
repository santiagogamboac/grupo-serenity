using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serenity.Application.Features.Auth;

namespace Serenity.Api.Controllers;

/// <summary>Sesión del panel admin: login, refresh (rotación), logout, perfil y cambio de contraseña.</summary>
[Route("api/auth")]
public sealed class AuthController(IMediator mediator) : ApiControllerBase(mediator)
{
    public sealed record RefreshRequest(string RefreshToken);

    public sealed record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
        => Responder(await Mediator.Send(command, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken ct)
        => Responder(await Mediator.Send(new RefreshTokenCommand(body.RefreshToken), ct));

    [HttpPost("logout")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Logout(CancellationToken ct)
        => UsuarioId is { } id ? Responder(await Mediator.Send(new LogoutCommand(id), ct)) : Unauthorized();

    [HttpGet("me")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => UsuarioId is { } id ? Responder(await Mediator.Send(new ObtenerUsuarioActualQuery(id), ct)) : Unauthorized();

    [HttpPost("cambiar-password")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest body, CancellationToken ct)
        => UsuarioId is { } id
            ? Responder(await Mediator.Send(new CambiarPasswordCommand(id, body.PasswordActual, body.PasswordNueva), ct))
            : Unauthorized();
}
