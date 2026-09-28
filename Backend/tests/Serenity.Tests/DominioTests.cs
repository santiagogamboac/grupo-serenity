using Serenity.Domain.Entities;

namespace Serenity.Tests;

public class UsuarioBloqueoTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Usuario Nuevo() => Usuario.Crear("Ana", "  ANA@Example.com ", "hash", "admin");

    [Fact]
    public void Crear_normaliza_correo()
    {
        var u = Nuevo();
        Assert.Equal("ana@example.com", u.Correo);
        Assert.True(u.Activo);
    }

    [Fact]
    public void Cuatro_fallos_no_bloquean()
    {
        var u = Nuevo();
        for (var i = 0; i < 4; i++)
        {
            Assert.False(u.RegistrarLoginFallido(T0));
        }

        Assert.False(u.EstaBloqueado(T0));
        Assert.Equal(4, u.IntentosFallidos);
    }

    [Fact]
    public void Quinto_fallo_bloquea_15_minutos()
    {
        var u = Nuevo();
        for (var i = 0; i < 4; i++) u.RegistrarLoginFallido(T0);

        Assert.True(u.RegistrarLoginFallido(T0));
        Assert.True(u.EstaBloqueado(T0));
        Assert.True(u.EstaBloqueado(T0.AddMinutes(14)));
        Assert.False(u.EstaBloqueado(T0.AddMinutes(15)));
        Assert.Equal(15, u.MinutosDeBloqueoRestantes(T0));
    }

    [Fact]
    public void Login_exitoso_reinicia_contador()
    {
        var u = Nuevo();
        for (var i = 0; i < 3; i++) u.RegistrarLoginFallido(T0);

        u.RegistrarLoginExitoso();

        Assert.Equal(0, u.IntentosFallidos);
        // Tras el éxito hacen falta otros 5 fallos completos para bloquear.
        for (var i = 0; i < 4; i++) Assert.False(u.RegistrarLoginFallido(T0));
        Assert.True(u.RegistrarLoginFallido(T0));
    }

    [Fact]
    public void Tras_vencer_el_bloqueo_se_empieza_de_cero()
    {
        var u = Nuevo();
        for (var i = 0; i < 5; i++) u.RegistrarLoginFallido(T0);

        var despues = T0.AddMinutes(16);
        Assert.False(u.EstaBloqueado(despues));
        Assert.False(u.RegistrarLoginFallido(despues));
        Assert.Equal(1, u.IntentosFallidos);
        Assert.Null(u.BloqueadoHasta);
    }
}
