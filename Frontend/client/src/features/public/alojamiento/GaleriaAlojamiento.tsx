import { useCallback, useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, Images, X } from "lucide-react";
import { TEXTOS } from "../textos";

export interface FotoDetalle {
  key: string;
  url: string;
  alt: string;
}

const T = TEXTOS.alojamiento;

/**
 * Mosaico tipo "foto grande + 4 miniaturas" en escritorio y carrusel con scroll-snap en móvil.
 * Cualquier foto abre el visor a pantalla completa (teclado: ← → Esc).
 */
export function GaleriaAlojamiento({ fotos, titulo }: { fotos: FotoDetalle[]; titulo: string }) {
  const [abierta, setAbierta] = useState<number | null>(null);
  const total = fotos.length;

  const mover = useCallback(
    (paso: number) => setAbierta(i => (i === null ? i : (i + paso + total) % total)),
    [total]
  );

  useEffect(() => {
    if (abierta === null) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setAbierta(null);
      if (e.key === "ArrowRight") mover(1);
      if (e.key === "ArrowLeft") mover(-1);
    };
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", onKey);
    };
  }, [abierta, mover]);

  if (total === 0) return <div className="detalle-galeria detalle-galeria--vacia" aria-hidden="true" />;
  const actual = abierta !== null ? fotos[abierta] : null;

  return (
    <>
      <div className="detalle-galeria" data-count={Math.min(total, 5)}>
        {fotos.map((f, i) => (
          <button
            key={f.key}
            type="button"
            className={`detalle-foto detalle-foto--${i}`}
            onClick={() => setAbierta(i)}
            aria-label={`${T.fotoDe(i + 1, total)}: ${f.alt || titulo}`}
          >
            <img
              src={f.url}
              alt={f.alt}
              loading={i === 0 ? "eager" : "lazy"}
              decoding="async"
              fetchPriority={i === 0 ? "high" : undefined}
            />
          </button>
        ))}
        {total > 1 && (
          <button type="button" className="detalle-ver-todas" onClick={() => setAbierta(0)}>
            <Images size={16} aria-hidden="true" /> {T.verFotos(total)}
          </button>
        )}
      </div>

      {actual && (
        <div
          className="lightbox"
          role="dialog"
          aria-modal="true"
          aria-label={actual.alt || titulo}
          onClick={() => setAbierta(null)}
        >
          <button className="lightbox-btn lightbox-close" onClick={() => setAbierta(null)} aria-label={TEXTOS.cerrar}>
            <X size={22} />
          </button>
          {total > 1 && (
            <button
              className="lightbox-btn lightbox-prev"
              onClick={e => {
                e.stopPropagation();
                mover(-1);
              }}
              aria-label={TEXTOS.anterior}
            >
              <ChevronLeft size={26} />
            </button>
          )}
          <figure onClick={e => e.stopPropagation()}>
            <img key={actual.url} src={actual.url} alt={actual.alt} />
            <figcaption>
              <strong>{titulo}</strong> · {T.fotoDe((abierta ?? 0) + 1, total)}
            </figcaption>
          </figure>
          {total > 1 && (
            <button
              className="lightbox-btn lightbox-next"
              onClick={e => {
                e.stopPropagation();
                mover(1);
              }}
              aria-label={TEXTOS.siguiente}
            >
              <ChevronRight size={26} />
            </button>
          )}
        </div>
      )}
    </>
  );
}
