import { ArrowUpRight, Star } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import { pad2 } from "@/lib/rich";
import type { ResenaPublica, ResenasContenido } from "@/lib/types";

function Estrellas({ cantidad, size }: { cantidad: number; size: number }) {
  return (
    <div className="stars">
      {[1, 2, 3, 4, 5].map(n => (
        <Star key={n} size={size} fill={n <= cantidad ? "currentColor" : "none"} />
      ))}
    </div>
  );
}

export function Resenas({ seccion, resenas }: { seccion: ResenasContenido; resenas: ResenaPublica[] }) {
  return (
    <section className="reviews-section section-pad" id="resenas">
      <div className="page-width reviews-layout">
        <div className="reviews-intro">
          <div className="eyebrow">
            <span>04</span>
            <i /> {seccion.etiqueta}
          </div>
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
            <a className="text-link" href={seccion.enlaceUrl}>
              {seccion.enlaceTexto} <ArrowUpRight size={16} />
            </a>
          )}
        </div>
        <div className="review-list">
          {resenas.map((r, i) => (
            <article className="review-card" key={r.id}>
              <div className="review-card-top">
                <span className="review-index">{pad2(i + 1)}</span>
                <Estrellas cantidad={r.calificacion} size={13} />
              </div>
              <p>“{r.texto}”</p>
              <div className="review-author">
                <div className={`avatar avatar--${(i % 3) + 1}`}>
                  <span>{r.autor.charAt(0)}</span>
                </div>
                <div>
                  <strong>{r.autor}</strong>
                  <span>{r.contexto}</span>
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
