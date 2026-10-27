import { useCallback, useEffect, useState, type CSSProperties } from "react";
import { ChevronLeft, ChevronRight, X } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import type { DestinosContenido, ProyectoPublico } from "@/lib/types";
import { TEXTOS } from "../textos";

interface Foto {
  key: string;
  url: string;
  alt: string;
  lugar: string;
  detalle: string;
}

/** Junta las fotos de todos los proyectos publicados (sin repetir) para el mosaico. */
function fotosDe(proyectos: ProyectoPublico[]): Foto[] {
  const vistas = new Set<string>();
  const fotos: Foto[] = [];
  for (const p of proyectos) {
    for (const img of p.imagenes) {
      if (vistas.has(img.url)) continue;
      vistas.add(img.url);
      fotos.push({ key: `${p.id}-${img.id}`, url: img.url, alt: img.textoAlt, lugar: p.nombre, detalle: p.ubicacionEtiqueta });
    }
  }
  return fotos;
}

export function Galeria({ seccion, proyectos }: { seccion: DestinosContenido; proyectos: ProyectoPublico[] }) {
  const fotos = fotosDe(proyectos);
  const [abierta, setAbierta] = useState<number | null>(null);

  const mover = useCallback(
    (paso: number) => setAbierta(i => (i === null ? i : (i + paso + fotos.length) % fotos.length)),
    [fotos.length]
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

  if (fotos.length === 0) return null;
  const actual = abierta !== null ? fotos[abierta] : null;

  return (
    <section className="gallery-section section-pad" id="galeria">
      <div className="page-width">
        <div className="gallery-heading" data-aparecer>
          {seccion.franjaEtiqueta && <div className="eyebrow">{seccion.franjaEtiqueta}</div>}
          <h2>
            <RichTitle text={seccion.franjaTitulo} />
          </h2>
        </div>

        <div className="gallery-grid cascada" data-aparecer data-count={Math.min(fotos.length, 6)}>
          {fotos.map((f, i) => (
            <button
              key={f.key}
              type="button"
              className={`gallery-tile gallery-tile--${i % 6}`}
              style={{ "--i": i % 8 } as CSSProperties}
              onClick={() => setAbierta(i)}
              aria-label={`${TEXTOS.verFoto}: ${f.alt || f.lugar}`}
            >
              <img src={f.url} alt={f.alt} loading="lazy" decoding="async" />
              <span className="gallery-caption">
                <strong>{f.lugar}</strong>
                <em>{f.detalle}</em>
              </span>
            </button>
          ))}
        </div>
      </div>

      {actual && (
        <div className="lightbox" role="dialog" aria-modal="true" aria-label={actual.alt || actual.lugar} onClick={() => setAbierta(null)}>
          <button className="lightbox-btn lightbox-close" onClick={() => setAbierta(null)} aria-label={TEXTOS.cerrar}>
            <X size={22} />
          </button>
          {fotos.length > 1 && (
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
              <strong>{actual.lugar}</strong> · {actual.detalle}
            </figcaption>
          </figure>
          {fotos.length > 1 && (
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
    </section>
  );
}
