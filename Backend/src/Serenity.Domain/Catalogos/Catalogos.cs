namespace Serenity.Domain;

/// <summary>
/// Listas cerradas del dominio. El frontend las consume por
/// <c>GET /api/admin/catalogos</c>, así que no se duplican allí.
/// </summary>
public static class Catalogos
{
    /// <summary>Claves de icono permitidas (se mapean a lucide-react en el frontend).</summary>
    public static readonly IReadOnlyList<string> Iconos =
    [
        "heart-handshake", "sparkles", "shield-check", "map-pin",
        "waves", "mountain", "sun", "trees", "tent", "sailboat",
        "compass", "leaf", "heart", "star", "home", "users", "bird", "coffee"
    ];

    public static readonly IReadOnlyList<string> VariantesValor = ["normal", "alta", "ancha"];
}

public static class EstadosSolicitud
{
    public const string Nueva = "nueva";
    public const string Contactada = "contactada";
    public const string Cerrada = "cerrada";
    public const string Descartada = "descartada";

    public static readonly IReadOnlyList<string> Todos = [Nueva, Contactada, Cerrada, Descartada];
}

public static class Roles
{
    public const string Admin = "admin";
}
