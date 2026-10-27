import { useEffect, useMemo } from "react";
import { useParams } from "wouter";
import { ArrowLeft, BedDouble, CalendarRange, MessageCircle, Sparkles, Users } from "lucide-react";
import { plainRich } from "@/lib/rich";
import { formatoPesos } from "@/lib/reservas";
import { TEXTOS } from "@/features/public/textos";
import { useAparecer } from "@/features/public/useAparecer";
import { useHome } from "@/features/public/useHome";
import { Pie } from "@/features/public/sections/Pie";
import { ReservaEnLinea } from "@/features/public/sections/ReservaEnLinea";
import { GaleriaAlojamiento, type FotoDetalle } from "@/features/public/alojamiento/GaleriaAlojamiento";
import { TarjetasAlojamientos } from "@/features/public/alojamiento/TarjetasAlojamientos";

const T = TEXTOS.alojamiento;

/** Detalle de un alojamiento reservable: fotos, ficha y reserva en línea. Los datos salen de GET /api/public/home. */
export default function AlojamientoPage() {
  const { id } = useParams<{ id: string }>();
  const { data, isPending, isError, refetch, isFetching } = useHome();

  const aloj = data?.reservaDirecta.alojamientos.find(a => a.id === Number(id));
  const proyecto = aloj ? data?.proyectos.find(p => p.id === aloj.proyectoId) : undefined;
  const raiz = useAparecer<HTMLElement>(Boolean(aloj && proyecto));

  // Portada + galería; sin fotos propias, las del destino.
  const fotos = useMemo<FotoDetalle[]>(() => {
    if (!aloj) return [];
    const propias: FotoDetalle[] = [
      ...(aloj.imagen ? [{ key: `p-${aloj.imagen.id}`, url: aloj.imagen.url, alt: aloj.nombre }] : []),
      ...aloj.imagenes.map(i => ({ key: `g-${i.id}`, url: i.url, alt: i.textoAlt })),
    ];
    if (propias.length > 0) return propias;
    return (proyecto?.imagenes ?? []).map(i => ({ key: `d-${i.id}`, url: i.url, alt: i.textoAlt }));
  }, [aloj, proyecto]);

  const marca = data?.contenido.marca.tituloSitio;
  useEffect(() => {
    if (aloj && marca) document.title = T.tituloPagina(aloj.nombre, plainRich(marca));
  }, [aloj, marca]);

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [id]);

  if (isPending) {
    return (
      <main className="home-status" aria-busy="true">
        <div className="home-status__spinner" />
        <span>{TEXTOS.cargando}</span>
      </main>
    );
  }

  if (isError) {
    return (
      <main className="home-status" role="alert">
        <strong>{TEXTOS.errorTitulo}</strong>
        <span>{TEXTOS.errorDetalle}</span>
        <button className="button button--solid" onClick={() => refetch()} disabled={isFetching}>
          {TEXTOS.reintentar}
        </button>
      </main>
    );
  }

  const c = data.contenido;

  if (!aloj || !proyecto) {
    return (
      <main className="home-status" role="alert">
        <strong>{T.noEncontrado}</strong>
        <span>{T.noEncontradoDetalle}</span>
        <a className="button button--solid" href="/#destinos">
          {T.volver}
        </a>
      </main>
    );
  }

  const parrafos = aloj.descripcion.split(/\n+/).filter(p => p.trim());

  return (
    <main className="site-shell site-shell--glass detalle-page" ref={raiz}>
      <div className="site-backdrop" aria-hidden="true">
        {fotos[0] && <img src={fotos[0].url} alt="" />}
      </div>

      <div className="site-frame">
        <header className="detalle-cabecera">
          <a className="detalle-volver" href="/#destinos">
            <ArrowLeft size={16} aria-hidden="true" /> {T.volver}
          </a>
          <a className="detalle-marca" href="/" aria-label={TEXTOS.inicioAria(c.marca.nombre)}>
            {c.marca.logo && <img src={c.marca.logo.url} alt={c.marca.logoAlt || c.marca.nombre} />}
          </a>
          {c.reservas.whatsappUrl ? (
            <a
              className="icon-glass"
              href={c.reservas.whatsappUrl}
              target="_blank"
              rel="noopener noreferrer"
              aria-label={TEXTOS.whatsapp}
            >
              <MessageCircle size={17} />
            </a>
          ) : (
            <span />
          )}
        </header>

        <GaleriaAlojamiento fotos={fotos} titulo={aloj.nombre} />

        <div className="page-width detalle-cuerpo">
          <article className="detalle-ficha">
            <div className="eyebrow">{proyecto.ubicacionEtiqueta || proyecto.nombre}</div>
            <h1>{aloj.nombre}</h1>
            <ul className="detalle-datos">
              <li>
                <Users size={18} aria-hidden="true" /> {T.capacidad(aloj.capacidad)}
              </li>
              <li>
                <BedDouble size={18} aria-hidden="true" /> {T.incluidos(aloj.huespedesIncluidos)}
              </li>
              <li>
                <CalendarRange size={18} aria-hidden="true" /> {T.minimo(aloj.estanciaMinima)}
              </li>
              <li>
                <Sparkles size={18} aria-hidden="true" />{" "}
                {aloj.tarifaAseo > 0 ? T.aseo(formatoPesos(aloj.tarifaAseo)) : T.sinAseo}
              </li>
            </ul>

            <p className="detalle-precio">
              {T.desde} <strong>{formatoPesos(aloj.precioNoche)}</strong> {T.porNoche}
              {aloj.recargoHuespedExtra > 0 && aloj.huespedesIncluidos < aloj.capacidad && (
                <span>{T.extra(formatoPesos(aloj.recargoHuespedExtra))}</span>
              )}
            </p>

            {parrafos.length > 0 && (
              <div className="detalle-descripcion">
                <h2>{T.detalles}</h2>
                {parrafos.map((p, i) => (
                  <p key={i}>{p}</p>
                ))}
              </div>
            )}
          </article>

          <aside className="detalle-reserva" id="reservar" aria-label={T.reservar}>
            <div className="booking-form">
              <ReservaEnLinea
                key={aloj.id}
                seccion={c.reservas}
                proyectos={data.proyectos}
                alojamientos={data.reservaDirecta.alojamientos}
                pagoEnLinea={data.reservaDirecta.pagoEnLinea}
                alojamientoFijoId={aloj.id}
              />
            </div>
          </aside>
        </div>

        <div className="page-width">
          <TarjetasAlojamientos
            alojamientos={data.reservaDirecta.alojamientos}
            proyectoId={proyecto.id}
            excluirId={aloj.id}
            titulo={T.otros}
          />
        </div>

        <Pie marca={c.marca} footer={c.footer} />
      </div>

      {/* Móvil: precio siempre a la vista y acceso directo a la reserva. */}
      <div className="detalle-barra">
        <span>
          {T.desde} <strong>{formatoPesos(aloj.precioNoche)}</strong> {T.porNoche}
        </span>
        <a className="pill-cta" href="#reservar">
          <span>{T.reservar}</span>
        </a>
      </div>
    </main>
  );
}
