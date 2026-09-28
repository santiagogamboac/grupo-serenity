using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serenity.Application.Interfaces;
using Serenity.Domain.Entities;

namespace Serenity.Infrastructure.Services;

/// <summary>Hash de contraseñas con BCrypt (work factor 11).</summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

/// <summary>Emite access tokens JWT (HS256) con la misma clave que valida JwtBearer en la API.</summary>
internal sealed class JwtTokenService : IJwtTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiraMinutos;
    private readonly int _refreshDias;

    public JwtTokenService(IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        _key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key no configurada.");
        _issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer no configurado.");
        _audience = jwt["Audience"] ?? throw new InvalidOperationException("Jwt:Audience no configurada.");
        _expiraMinutos = int.TryParse(jwt["ExpiresInMinutes"], out var m) ? m : 60;
        _refreshDias = int.TryParse(jwt["RefreshExpiresInDays"], out var d) ? d : 7;
    }

    public AccessTokenGenerado GenerarAccessToken(Usuario usuario)
    {
        var ahora = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Correo),
            new Claim(JwtRegisteredClaimNames.Name, usuario.Nombre),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // ClaimTypes.Role para que funcione RequireRole("admin").
            new Claim(ClaimTypes.Role, usuario.Rol)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: ahora,
            expires: ahora.AddMinutes(_expiraMinutos),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256));

        return new AccessTokenGenerado(
            new JwtSecurityTokenHandler().WriteToken(token),
            (int)TimeSpan.FromMinutes(_expiraMinutos).TotalSeconds);
    }

    public RefreshTokenGenerado GenerarRefreshToken()
        => new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), DateTime.UtcNow.AddDays(_refreshDias));
}
