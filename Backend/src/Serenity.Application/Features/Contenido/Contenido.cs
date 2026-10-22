using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Features.Media;
using Serenity.Application.Interfaces;

namespace Serenity.Application.Features.Contenido;

// ─── Validadores por sección ──────────────────────────────────────────────────

public sealed class MarcaValidator : AbstractValidator<MarcaContenido>
{
    public MarcaValidator()
    {
        RuleFor(x => x.Nombre).Texto(120);
        RuleFor(x => x.TituloSitio).Texto(160);
        RuleFor(x => x.Leyenda).Texto(120, requerido: false);
        RuleFor(x => x.LogoAlt).Texto(200, requerido: false);
    }
}

public sealed class NavegacionValidator : AbstractValidator<NavegacionContenido>
{
    public NavegacionValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(i => i.Count is >= 1 and <= 8)
            .WithMessage("La navegación debe tener entre 1 y 8 enlaces.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(e => e.Texto).Texto(40);
            i.RuleFor(e => e.Href).NotEmpty().MaximumLength(500).EnlaceSeguro();
        });
        RuleFor(x => x.CtaTexto).Texto(40);
        RuleFor(x => x.CtaHref).NotEmpty().MaximumLength(500).EnlaceSeguro();
    }
}

public sealed class AnuncioValidator : AbstractValidator<AnuncioContenido>
{
    public AnuncioValidator()
    {
        RuleFor(x => x.Texto).Texto(200, requerido: false);
        RuleFor(x => x.CtaTexto).Texto(60, requerido: false);
        RuleFor(x => x.CtaHref).MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.Texto).NotEmpty().When(x => x.Activo).WithMessage("El anuncio activo necesita texto.");
    }
}

public sealed class HeroValidator : AbstractValidator<HeroContenido>
{
    public HeroValidator()
    {
        RuleFor(x => x.Kicker).Texto(80);
        RuleFor(x => x.Titulo).Texto(200);
        RuleFor(x => x.Texto).Texto(500);
        RuleFor(x => x.CtaPrimarioTexto).Texto(60);
        RuleFor(x => x.CtaPrimarioHref).NotEmpty().MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.CtaSecundarioTexto).Texto(60);
        RuleFor(x => x.CtaSecundarioHref).NotEmpty().MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.ImagenPrincipalAlt).Texto(200, requerido: false);
        RuleFor(x => x.ImagenSecundariaAlt).Texto(200, requerido: false);
        RuleFor(x => x.NotaDestinos).Texto(80, requerido: false);
        RuleFor(x => x.SelloLinea1).Texto(30, requerido: false);
        RuleFor(x => x.SelloCentral).Texto(30, requerido: false);
        RuleFor(x => x.SelloLinea3).Texto(30, requerido: false);
    }
}

public sealed class IntroValidator : AbstractValidator<IntroContenido>
{
    public IntroValidator()
    {
        RuleFor(x => x.Etiqueta).Texto(80);
        RuleFor(x => x.Titulo).Texto(200);
        RuleFor(x => x.Lead).Texto(400);
        RuleFor(x => x.Texto).Texto(1000);
        RuleFor(x => x.EnlaceTexto).Texto(60, requerido: false);
        RuleFor(x => x.EnlaceHref).MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.Cita).Texto(400);
        RuleFor(x => x.Firma).Texto(80, requerido: false);
    }
}

public sealed class ValoresValidator : AbstractValidator<ValoresContenido>
{
    public ValoresValidator()
    {
        RuleFor(x => x.Etiqueta).Texto(80);
        RuleFor(x => x.Titulo).Texto(200);
        RuleFor(x => x.Texto).Texto(400, requerido: false);
    }
}

public sealed class DestinosValidator : AbstractValidator<DestinosContenido>
{
    public DestinosValidator()
    {
        RuleFor(x => x.Etiqueta).Texto(80);
        RuleFor(x => x.Titulo).Texto(200);
        RuleFor(x => x.Texto).Texto(400, requerido: false);
        RuleFor(x => x.FranjaEtiqueta).Texto(80, requerido: false);
        RuleFor(x => x.FranjaTitulo).Texto(120, requerido: false);
        RuleFor(x => x.ChipMarca).Texto(40, requerido: false);
    }
}

public sealed class ResenasValidator : AbstractValidator<ResenasContenido>
{
    public ResenasValidator()
    {
        RuleFor(x => x.Etiqueta).Texto(80);
        RuleFor(x => x.Titulo).Texto(200);
        RuleFor(x => x.Calificacion).Texto(10, requerido: false);
        RuleFor(x => x.Detalle).Texto(160, requerido: false);
        RuleFor(x => x.EnlaceTexto).Texto(60, requerido: false);
        RuleFor(x => x.EnlaceUrl).MaximumLength(500).EnlaceSeguro();
    }
}

public sealed class ReservasValidator : AbstractValidator<ReservasContenido>
{
    public ReservasValidator()
    {
        RuleFor(x => x.Etiqueta).Texto(80);
        RuleFor(x => x.Titulo).Texto(200);
        RuleFor(x => x.Texto).Texto(400);
        RuleFor(x => x.WhatsappTexto).Texto(60, requerido: false);
        RuleFor(x => x.WhatsappUrl).MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.Nota).Texto(160, requerido: false);
        RuleFor(x => x.FormTitulo).Texto(80);
        RuleFor(x => x.FormSubtitulo).Texto(80, requerido: false);
        RuleFor(x => x.BotonTexto).Texto(60);
        RuleFor(x => x.Aviso).Texto(240, requerido: false);
        RuleFor(x => x.ExitoTitulo).Texto(120);
        RuleFor(x => x.ExitoDescripcion).Texto(300);
        RuleFor(x => x.ReservaTitulo).Texto(80, requerido: false);
        RuleFor(x => x.ReservaSubtitulo).Texto(80, requerido: false);
        RuleFor(x => x.ReservaBotonTexto).Texto(60, requerido: false);
        RuleFor(x => x.Condiciones).Texto(1000, requerido: false);
        RuleFor(x => x.ReservaSinPagoTexto).Texto(300, requerido: false);
    }
}

public sealed class FooterValidator : AbstractValidator<FooterContenido>
{
    public FooterValidator()
    {
        RuleFor(x => x.Eslogan).Texto(200);
        RuleFor(x => x.RedesTitulo).Texto(60, requerido: false);
        RuleFor(x => x.FacebookUrl).MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.InstagramUrl).MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.YoutubeUrl).MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.Copyright).Texto(120);
        RuleFor(x => x.Frase).Texto(120, requerido: false);
        RuleFor(x => x.SitioActualTexto).Texto(60, requerido: false);
        RuleFor(x => x.SitioActualUrl).MaximumLength(500).EnlaceSeguro();
    }
}

// ─── Carga compartida (admin y público) ───────────────────────────────────────

/// <summary>Lee todas las secciones, completa las que falten y expande las imágenes.</summary>
public sealed class CargadorContenido(ISeccionRepository secciones, IMediaRepository media, IAlmacenamientoObjetos almacenamiento)
{
    public async Task<IReadOnlyDictionary<string, ISeccionContenido>> CargarAsync(bool paraPublico, CancellationToken ct)
    {
        var crudas = await secciones.ObtenerTodasAsync(ct);

        var lista = SeccionesRegistro.Claves
            .ToDictionary(c => c, c => SeccionesRegistro.DeserializarOVacia(c, crudas.GetValueOrDefault(c)));

        var ids = lista.Values.SelectMany(s => s.MediaIds()).Distinct().ToList();
        var mediaPorId = (await media.ObtenerPorIdsAsync(ids, ct))
            .ToDictionary(m => m.Id, m => new MediaDto(m.Id, almacenamiento.ConstruirUrlPublica(m.StorageKey), m.NombreOriginal));

        return lista.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var conMedia = kv.Value.ConMedia(mediaPorId);
                return paraPublico ? conMedia.ParaPublico() : conMedia;
            });
    }
}

public sealed record ObtenerContenidoAdminQuery : IRequest<IReadOnlyDictionary<string, object>>;

public sealed class ObtenerContenidoAdminQueryHandler(CargadorContenido cargador)
    : IRequestHandler<ObtenerContenidoAdminQuery, IReadOnlyDictionary<string, object>>
{
    public async Task<IReadOnlyDictionary<string, object>> Handle(ObtenerContenidoAdminQuery request, CancellationToken ct)
        // object (no la interfaz) para que System.Text.Json serialice el tipo concreto de cada sección.
        => (await cargador.CargarAsync(paraPublico: false, ct)).ToDictionary(kv => kv.Key, kv => (object)kv.Value);
}

// ─── Guardar sección ──────────────────────────────────────────────────────────

public sealed record GuardarSeccionCommand(string Clave, ISeccionContenido Contenido) : IRequest<Result<object>>;

public sealed class GuardarSeccionCommandValidator : AbstractValidator<GuardarSeccionCommand>
{
    public GuardarSeccionCommandValidator()
    {
        RuleFor(x => x.Contenido).SetInheritanceValidator(v =>
        {
            v.Add(new MarcaValidator());
            v.Add(new NavegacionValidator());
            v.Add(new AnuncioValidator());
            v.Add(new HeroValidator());
            v.Add(new IntroValidator());
            v.Add(new ValoresValidator());
            v.Add(new DestinosValidator());
            v.Add(new ResenasValidator());
            v.Add(new ReservasValidator());
            v.Add(new FooterValidator());
        });
    }
}

public sealed class GuardarSeccionCommandHandler(
    ISeccionRepository secciones,
    IMediaRepository media,
    CargadorContenido cargador) : IRequestHandler<GuardarSeccionCommand, Result<object>>
{
    public async Task<Result<object>> Handle(GuardarSeccionCommand request, CancellationToken ct)
    {
        if (!SeccionesRegistro.Tipos.TryGetValue(request.Clave, out var tipo) || request.Contenido.GetType() != tipo)
        {
            return Result.Failure<object>($"La sección '{request.Clave}' no existe.", ErrorTipo.NoEncontrado);
        }

        var ids = request.Contenido.MediaIds().Distinct().ToList();
        if (ids.Count > 0)
        {
            var existentes = (await media.ObtenerPorIdsAsync(ids, ct)).Select(m => m.Id).ToHashSet();
            var faltantes = ids.Where(i => !existentes.Contains(i)).ToList();
            if (faltantes.Count > 0)
            {
                return Result.Failure<object>($"La imagen {faltantes[0]} no existe en la biblioteca.");
            }
        }

        await secciones.GuardarAsync(request.Clave, SeccionesRegistro.Serializar(request.Contenido), ct);

        var actualizadas = await cargador.CargarAsync(paraPublico: false, ct);
        return Result.Success<object>(actualizadas[request.Clave]);
    }
}
