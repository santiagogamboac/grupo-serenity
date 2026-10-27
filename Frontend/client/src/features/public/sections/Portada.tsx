import { useEffect, useRef, type CSSProperties, type ReactNode } from "react";
import { ArrowDown, ArrowRight, MapPin, Star } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import { iconoDe } from "@/lib/icons";
import { pad2 } from "@/lib/rich";
import type { HeroContenido, IntroContenido, ProyectoPublico, ResenasContenido } from "@/lib/types";

/** Desplaza la foto de portada más lento que el scroll (parallax suave); respeta "reducir movimiento". */
function useParallax<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  useEffect(() => {
    const el = ref.current;
    if (!el || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    let raf = 0;
    const onScroll = () => {
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => {
        const y = Math.min(window.scrollY, window.innerHeight * 1.2);
        el.style.transform = `translate3d(0, ${(y * 0.22).toFixed(1)}px, 0)`;
      });
    };
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener("scroll", onScroll);
    };
  }, []);
  return ref;
}

/** Palabra gigante de la portada: cada letra entra con un pequeño retraso. */
function Wordmark({ texto }: { texto: string }) {
  const palabras = texto.trim().split(/\s+/);
  const largo = Math.max(...palabras.map(p => p.length));
  let indice = 0;
  return (
    <p
      className="hero-wordmark"
      aria-hidden="true"
      style={{ "--chars": texto.trim().length, "--largo": largo } as CSSProperties}
    >
      {palabras.map((palabra, p) => (
        <span className="hero-wordmark__palabra" key={p}>
          {Array.from(palabra).map((letra, l) => (
            <span key={l} style={{ "--i": indice++ } as CSSProperties}>
              {letra}
            </span>
          ))}
        </span>
      ))}
    </p>
  );
}

export function Portada({
  hero,
  resenas,
  proyectos,
  cabecera,
}: {
  hero: HeroContenido;
  resenas: ResenasContenido;
  proyectos: ProyectoPublico[];
  cabecera: ReactNode;
}) {
  const media = useParallax<HTMLDivElement>();
  const sello = [hero.selloLinea1, hero.selloCentral].filter(Boolean).join(" ");

  return (
    <section className="hero" id="inicio">
      <div className="hero-media" ref={media} aria-hidden="true">
        {hero.imagenPrincipal && (
          <img className="hero-image" src={hero.imagenPrincipal.url} alt={hero.imagenPrincipalAlt} fetchPriority="high" />
        )}
      </div>
      <div className="hero-vignette" aria-hidden="true" />
      {cabecera}

      <div className="hero-inner">
        <div className="hero-top">
          {hero.kicker && <Wordmark texto={hero.kicker} />}
        </div>

        <div className="hero-bottom">
          <div className="hero-content">
            <h1>
              <RichTitle text={hero.titulo} />
            </h1>
            {hero.texto && <p className="hero-copy">{hero.texto}</p>}
            {hero.ctaSecundarioTexto && (
              <a className="text-link text-link--light" href={hero.ctaSecundarioHref}>
                {hero.ctaSecundarioTexto} <ArrowDown size={16} />
              </a>
            )}
            {(sello || hero.selloLinea3) && (
              <div className="hero-footnote">
                <span>{sello}</span>
                <i aria-hidden="true" />
                <span>{hero.selloLinea3}</span>
              </div>
            )}
          </div>

          <a className="pill-cta pill-cta--grande hero-cta" href={hero.ctaPrimarioHref}>
            <span>{hero.ctaPrimarioTexto}</span>
            <i aria-hidden="true">
              <ArrowRight size={18} />
            </i>
          </a>

          <ul className="hero-stats">
            {resenas.calificacion && (
              <li className="glass-stat">
                <strong>
                  {resenas.calificacion}
                  <Star size={18} fill="currentColor" aria-hidden="true" />
                </strong>
                <span>{resenas.detalle}</span>
              </li>
            )}
            {proyectos.length > 0 && (
              <li className="glass-stat">
                <strong>{pad2(proyectos.length)}</strong>
                <span>{hero.notaDestinos}</span>
              </li>
            )}
            {proyectos.length > 0 && (
              <li className="glass-stat glass-stat--lugares">
                <div className="glass-stat__iconos" aria-hidden="true">
                  {proyectos.slice(0, 4).map(p => {
                    const Icono = iconoDe(p.icono);
                    return <Icono key={p.id} size={16} />;
                  })}
                </div>
                <b>
                  <MapPin size={14} aria-hidden="true" /> {proyectos.map(p => p.nombre).join(" · ")}
                </b>
              </li>
            )}
          </ul>
        </div>
      </div>
    </section>
  );
}

export function Experiencia({ intro, hero }: { intro: IntroContenido; hero: HeroContenido }) {
  const foto = hero.imagenSecundaria;
  return (
    <section className="intro-section section-pad" id="experiencia">
      <div className="glow-orb glow-orb--sun" aria-hidden="true" />
      <div className="page-width intro-grid">
        <div className="intro-main" data-aparecer>
          <div className="eyebrow">{intro.etiqueta}</div>
          <h2>
            <RichTitle text={intro.titulo} />
          </h2>
          <p className="intro-lead">{intro.lead}</p>
          <div className="intro-body">
            <p>{intro.texto}</p>
            {intro.enlaceTexto && (
              <a className="pill-cta pill-cta--glass" href={intro.enlaceHref}>
                <span>{intro.enlaceTexto}</span>
                <i aria-hidden="true">
                  <ArrowRight size={16} />
                </i>
              </a>
            )}
          </div>
        </div>
        <figure className={`intro-visual ${foto ? "" : "intro-visual--sin-foto"}`} data-aparecer>
          {foto && <img src={foto.url} alt={hero.imagenSecundariaAlt} loading="lazy" decoding="async" />}
          {intro.cita && (
            <figcaption className="intro-quote glass-card">
              <span className="quote-mark" aria-hidden="true">
                “
              </span>
              <p>{intro.cita}</p>
              {intro.firma && <span className="signature">{intro.firma}</span>}
            </figcaption>
          )}
        </figure>
      </div>
    </section>
  );
}
