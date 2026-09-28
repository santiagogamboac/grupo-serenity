import { ArrowDown, ArrowUpRight } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import { pad2 } from "@/lib/rich";
import type { HeroContenido, IntroContenido } from "@/lib/types";

export function Portada({ hero, totalDestinos }: { hero: HeroContenido; totalDestinos: number }) {
  return (
    <section className="hero" id="inicio">
      <div className="hero-media" aria-hidden="true">
        {hero.imagenPrincipal && (
          <img className="hero-image hero-image--main" src={hero.imagenPrincipal.url} alt={hero.imagenPrincipalAlt} />
        )}
        {hero.imagenSecundaria && (
          <img className="hero-image hero-image--secondary" src={hero.imagenSecundaria.url} alt={hero.imagenSecundariaAlt} />
        )}
        <div className="hero-vignette" />
      </div>
      <div className="hero-content page-width">
        <div className="hero-kicker">
          <span /> {hero.kicker} <span />
        </div>
        <h1>
          <RichTitle text={hero.titulo} />
        </h1>
        <p className="hero-copy">{hero.texto}</p>
        <div className="hero-actions">
          <a className="button button--light" href={hero.ctaPrimarioHref}>
            {hero.ctaPrimarioTexto} <ArrowUpRight size={17} />
          </a>
          <a className="text-link text-link--light" href={hero.ctaSecundarioHref}>
            {hero.ctaSecundarioTexto} <ArrowDown size={17} />
          </a>
        </div>
        <div className="hero-footnote">
          <span>01</span>
          <div className="hero-progress">
            <i />
          </div>
          <span>
            {pad2(totalDestinos)} {hero.notaDestinos}
          </span>
        </div>
      </div>
      {(hero.selloLinea1 || hero.selloCentral || hero.selloLinea3) && (
        <div className="hero-stamp" aria-hidden="true">
          <span>{hero.selloLinea1}</span>
          <strong>{hero.selloCentral}</strong>
          <span>{hero.selloLinea3}</span>
        </div>
      )}
    </section>
  );
}

export function Experiencia({ intro }: { intro: IntroContenido }) {
  return (
    <section className="intro-section section-pad" id="experiencia">
      <div className="page-width intro-grid">
        <div className="eyebrow">
          <span>01</span>
          <i /> {intro.etiqueta}
        </div>
        <div className="intro-main">
          <h2>
            <RichTitle text={intro.titulo} />
          </h2>
          <p className="intro-lead">{intro.lead}</p>
          <div className="intro-body">
            <p>{intro.texto}</p>
            {intro.enlaceTexto && (
              <a className="text-link" href={intro.enlaceHref}>
                {intro.enlaceTexto} <ArrowUpRight size={16} />
              </a>
            )}
          </div>
        </div>
        <div className="intro-aside">
          <div className="quote-mark">“</div>
          <p>{intro.cita}</p>
          <div className="signature">{intro.firma}</div>
        </div>
      </div>
    </section>
  );
}
