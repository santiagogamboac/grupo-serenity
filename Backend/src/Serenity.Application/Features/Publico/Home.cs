using MediatR;
using Serenity.Application.Features.Contenido;
using Serenity.Application.Features.Proyectos;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Domain;

namespace Serenity.Application.Features.Publico;

public sealed record ProyectoImagenPublicaDto(long Id, string Url, string TextoAlt);

public sealed record ProyectoPublicoDto(
    long Id, string Slug, string Nombre, string Etiqueta, string Icono, string UbicacionEtiqueta,
    string Titulo, string Descripcion, string MetaLugar, string MetaEstancia, string? UrlReserva,
    string CtaTexto, IReadOnlyList<ProyectoImagenPublicaDto> Imagenes);

public sealed record ValorPublicoDto(long Id, string Titulo, string Descripcion, string Icono, string Variante);

public sealed record ResenaPublicaDto(long Id, string Autor, string Contexto, string Texto, int Calificacion);

public sealed record HomeDto(
    IReadOnlyDictionary<string, object> Contenido,
    IReadOnlyList<ProyectoPublicoDto> Proyectos,
    IReadOnlyList<ValorPublicoDto> Valores,
    IReadOnlyList<ResenaPublicaDto> Resenas);

public sealed record ObtenerHomeQuery : IRequest<HomeDto>;

public sealed class ObtenerHomeQueryHandler(
    CargadorContenido contenido,
    IProyectoRepository proyectos,
    ProyectoEnsamblador ensamblador,
    IValorRepository valores,
    IResenaRepository resenas) : IRequestHandler<ObtenerHomeQuery, HomeDto>
{
    public async Task<HomeDto> Handle(ObtenerHomeQuery request, CancellationToken ct)
    {
        // Secuencial: comparten conexión por request y son consultas pequeñas.
        var secciones = await contenido.CargarAsync(paraPublico: true, ct);
        var proyectosDto = await ensamblador.EnsamblarAsync(await proyectos.ListarAsync(soloPublicados: true, ct), ct);
        var valoresDto = await valores.ListarAsync(soloActivos: true, ct);
        var resenasDto = await resenas.ListarAsync(soloPublicadas: true, ct);

        return new HomeDto(
            // object (no la interfaz) para que System.Text.Json serialice el tipo concreto de cada sección.
            secciones.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            proyectosDto.Select(p => new ProyectoPublicoDto(
                p.Id, p.Slug, p.Nombre, p.Etiqueta, p.Icono, p.UbicacionEtiqueta, p.Titulo, p.Descripcion,
                p.MetaLugar, p.MetaEstancia, p.UrlReserva, p.CtaTexto,
                p.Imagenes.Select(i => new ProyectoImagenPublicaDto(i.Id, i.Url, i.TextoAlt)).ToList())).ToList(),
            valoresDto.Select(v => new ValorPublicoDto(v.Id, v.Titulo, v.Descripcion, v.Icono, v.Variante)).ToList(),
            resenasDto.Select(r => new ResenaPublicaDto(r.Id, r.Autor, r.Contexto, r.Texto, r.Calificacion)).ToList());
    }
}

// ─── Admin: dashboard y catálogos ─────────────────────────────────────────────

public sealed record ObtenerDashboardQuery : IRequest<DashboardDto>;

public sealed class ObtenerDashboardQueryHandler(IDashboardRepository dashboard) : IRequestHandler<ObtenerDashboardQuery, DashboardDto>
{
    public Task<DashboardDto> Handle(ObtenerDashboardQuery request, CancellationToken ct) => dashboard.ObtenerAsync(ct);
}

public sealed record CatalogosDto(
    IReadOnlyList<string> Iconos,
    IReadOnlyList<string> VariantesValor,
    IReadOnlyList<string> EstadosSolicitud,
    IReadOnlyList<string> Secciones);

public sealed record ObtenerCatalogosQuery : IRequest<CatalogosDto>;

public sealed class ObtenerCatalogosQueryHandler : IRequestHandler<ObtenerCatalogosQuery, CatalogosDto>
{
    public Task<CatalogosDto> Handle(ObtenerCatalogosQuery request, CancellationToken ct)
        => Task.FromResult(new CatalogosDto(
            Catalogos.Iconos, Catalogos.VariantesValor, EstadosSolicitud.Todos, SeccionesRegistro.Claves.ToList()));
}
