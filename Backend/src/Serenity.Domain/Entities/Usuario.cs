namespace Serenity.Domain.Entities;

/// <summary>
/// Usuario del panel de administración. Las reglas de bloqueo por intentos
/// fallidos viven aquí, no en los handlers.
/// </summary>
public sealed class Usuario
{
    public const int MaxIntentosFallidos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    // Dapper hidrata por el constructor privado + setters privados.
    private Usuario() { }

    public long Id { get; private set; }
    public string Nombre { get; private set; } = default!;
    public string Correo { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string Rol { get; private set; } = default!;
    public bool Activo { get; private set; }
    public int IntentosFallidos { get; private set; }
    public DateTime? BloqueadoHasta { get; private set; }

    public static Usuario Crear(string nombre, string correo, string passwordHash, string rol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(rol);

        return new Usuario
        {
            Nombre = nombre.Trim(),
            Correo = correo.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Rol = rol,
            Activo = true
        };
    }

    public bool EstaBloqueado(DateTime ahoraUtc)
        => BloqueadoHasta is { } hasta && hasta > ahoraUtc;

    public int MinutosDeBloqueoRestantes(DateTime ahoraUtc)
        => BloqueadoHasta is { } hasta && hasta > ahoraUtc
            ? (int)Math.Ceiling((hasta - ahoraUtc).TotalMinutes)
            : 0;

    /// <summary>Registra un login fallido. Devuelve true si este fallo dispara el bloqueo.</summary>
    public bool RegistrarLoginFallido(DateTime ahoraUtc)
    {
        // Si el bloqueo anterior ya venció, se empieza de cero.
        if (BloqueadoHasta is { } previo && previo <= ahoraUtc)
        {
            BloqueadoHasta = null;
            IntentosFallidos = 0;
        }

        IntentosFallidos++;

        if (IntentosFallidos < MaxIntentosFallidos)
        {
            return false;
        }

        BloqueadoHasta = ahoraUtc.Add(DuracionBloqueo);
        IntentosFallidos = 0;
        return true;
    }

    public void RegistrarLoginExitoso()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }
}
