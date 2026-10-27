import { useEffect, useState } from "react";
import { ArrowRight, ChevronLeft, ChevronRight, Star } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import type { ResenaPublica, ResenasContenido } from "@/lib/types";
import { TEXTOS } from "../textos";

const INTERVALO_MS = 7000;

function Estrellas({ cantidad, size }: { cantidad: number; size: number }) {
  return (
    <div className="stars" aria-hidden="true">
      {[1, 2, 3, 4, 5].map(n => (
        <Star key={n} size={size} fill={n <= cantidad ? "currentColor" : "none"} />
      ))}
    </div>
  );
}

export function Resenas({ seccion, resenas }: { seccion: ResenasContenido; resenas: ResenaPublica[] }) {
  const [actual, setActual] = useState(0);
  const [pausado, setPausado] = useState(false);
  const total = resenas.length;
  const indice = total > 0 ? Math.min(actual, total - 1) : 0;
  const mover = (paso: number) => setActual(i => (Math.min(i, total - 1) + paso + total) % total);

  // Avance automático: se detiene con el puntero o el foco encima, con la pestaña oculta o si se pidió reducir movimiento.
  useEffect(() => {
    if (total < 2 || pausado || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const id = window.setInterval(() => {
      if (!document.hidden) setActual(i => (Math.min(i, total - 1) + 1) % total);
    }, INTERVALO_MS);
    return () => window.clearInterval(id);
  }, [total, pausado]);

  return (
    <section className="reviews-section section-pad" id="resenas">
      <div className="page-width reviews-layout">
        <div className="reviews-intro" data-aparecer>
          <div className="eyebrow">{seccion.etiqueta}</div>
          <h2>
            <RichTitle text={seccion.titulo} />
          </h2>
          {seccion.calificacion && (
            <div className="rating">
              <strong>{seccion.calificacion}</strong>
              <div>
                <Estrellas cantidad={Math.round(Number.parseFloat(seccion.calificacion.replace(",", ".")) || 5)} size={16} />
                <span>{seccion.detalle}</span>
              </div>
            </div>
          )}
          {seccion.enlaceUrl && (
            <a className="pill-cta pill-cta--glass" href={seccion.enlaceUrl} target="_blank" rel="noopener noreferrer">
              <span>{seccion.enlaceTexto}</span>
              <i aria-hidden="true">
                <ArrowRight size={16} />
              </i>
            </a>
          )}
        </div>

        {total > 0 && (
          <div
            className="review-carousel glass-card"
            role="region"
            aria-roledescription="carousel"
            aria-label={TEXTOS.resenasAria}
            data-aparecer
            onMouseEnter={() => setPausado(true)}
            onMouseLeave={() => setPausado(false)}
            onFocus={() => setPausado(true)}
            onBlur={() => setPausado(false)}
          >
            <span className="review-quote" aria-hidden="true">
              “
            </span>
            <div className="review-stage" aria-live={pausado ? "polite" : "off"}>
              {resenas.map((r, i) => (
                <article
                  key={r.id}
                  className={`review-slide ${i === indice ? "is-activa" : ""}`}
                  role="group"
                  aria-roledescription="slide"
                  aria-label={TEXTOS.resenaN(i + 1, total)}
                  aria-hidden={i !== indice}
                >
                  <p>{r.texto}</p>
                  <div className="review-author">
                    <div className={`avatar avatar--${(i % 3) + 1}`} aria-hidden="true">
                      {r.autor.charAt(0)}
                    </div>
                    <div>
                      <strong>{r.autor}</strong>
                      <span>{r.contexto}</span>
                    </div>
                    <Estrellas cantidad={r.calificacion} size={14} />
                  </div>
                </article>
              ))}
            </div>
            {total > 1 && (
              <div className="review-controls">
                <button type="button" className="icon-glass" onClick={() => mover(-1)} aria-label={TEXTOS.resenaAnterior}>
                  <ChevronLeft size={20} />
                </button>
                <div className="review-dots" aria-hidden="true">
                  {resenas.map((r, i) => (
                    <span key={r.id} className={i === indice ? "is-activa" : ""} />
                  ))}
                </div>
                <button type="button" className="icon-glass" onClick={() => mover(1)} aria-label={TEXTOS.resenaSiguiente}>
                  <ChevronRight size={20} />
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
