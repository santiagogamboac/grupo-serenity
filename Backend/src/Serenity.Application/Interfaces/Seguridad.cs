using Serenity.Domain.Entities;

namespace Serenity.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed record AccessTokenGenerado(string Token, int ExpiraEnSegundos);

public sealed record RefreshTokenGenerado(string Token, DateTime ExpiraEnUtc);

public interface IJwtTokenService
{
    AccessTokenGenerado GenerarAccessToken(Usuario usuario);
    RefreshTokenGenerado GenerarRefreshToken();
}
