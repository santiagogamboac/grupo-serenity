import { useState } from "react";
import { ArrowRight, Clock3, MapPin } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import { iconoDe } from "@/lib/icons";
import { pad2 } from "@/lib/rich";
import type { AlojamientoPublico, DestinosContenido, ProyectoPublico } from "@/lib/types";
import { TarjetasAlojamientos } from "../alojamiento/TarjetasAlojamientos";
import { TEXTOS } from "../textos";

interface Props {
  seccion: DestinosContenido;
  proyectos: ProyectoPublico[];
  /** Proyectos con alojamientos que se reservan en el sitio: su botón lleva a #reservas con el destino elegido. */
  reservables: Set<number>;
  onReservar: (proyectoId: number) => void;
  /** Alojamientos reservables: los del destino activo se listan con enlace a su detalle. */
  alojamientos?: AlojamientoPublico[];
}

export function Destinos({ seccion, proyectos, reservables, onReservar, alojamientos = [] }: Props) {
  const [seleccion, setSeleccion] = useState(0);

  if (proyectos.length === 0) return null;

  // Si el admin despublica proyectos mientras la página está abierta, el índice se ajusta.
  const indice = Math.min(seleccion, proyectos.length - 1);
  const activo = proyectos[indice];
  const foto = activo.imagenes[0];
  const reservable = reservables.has(activo.id);
  const enlace = reservable ? "#reservas" : activo.urlReserva;

  return (
    <section className="destinations-section section-pad" id="destinos">
      <div className="page-width">
        <div className="section-heading section-heading--destinations" data-aparecer>
          <div>
            <div className="eyebrow">{seccion.etiqueta}</div>
            <h2>
              <RichTitle text={seccion.titulo} />
            </h2>
          </div>
          {seccion.texto && (
            <p>
              <RichTitle text={seccion.texto} />
            </p>
          )}
        </div>

        <div data-aparecer>
          {proyectos.length > 1 && (
            <div className="destination-switch" role="tablist" aria-label={TEXTOS.destinosAria}>
              {proyectos.map((p, i) => {
                const Icono = iconoDe(p.icono);
                return (
                  <button
                    key={p.id}
                    type="button"
                    id={`destino-tab-${p.id}`}
                    className={i === indice ? "is-active" : ""}
                    onClick={() => setSeleccion(i)}
                    role="tab"
                    aria-selected={i === indice}
                    aria-controls="destino-panel"
                  >
                    <Icono size={17} aria-hidden="true" /> {p.etiqueta}
                  </button>
                );
              })}
            </div>
          )}

          <div
            className="destination-feature"
            id="destino-panel"
            role={proyectos.length > 1 ? "tabpanel" : undefined}
            aria-labelledby={proyectos.length > 1 ? `destino-tab-${activo.id}` : undefined}
          >
            <div className="destination-copy" key={`copia-${activo.id}`}>
              <div className="destination-label">{activo.ubicacionEtiqueta}</div>
              <h3>{activo.titulo}</h3>
              <p>{activo.descripcion}</p>
              {(activo.metaLugar || activo.metaEstancia) && (
                <ul className="destination-meta">
                  {activo.metaLugar && (
                    <li>
                      <MapPin size={15} aria-hidden="true" /> {activo.metaLugar}
                    </li>
                  )}
                  {activo.metaEstancia && (
                    <li>
                      <Clock3 size={15} aria-hidden="true" /> {activo.metaEstancia}
                    </li>
                  )}
                </ul>
              )}
              {enlace && (
                <a className="pill-cta" href={enlace} onClick={reservable ? () => onReservar(activo.id) : undefined}>
                  <span>{activo.ctaTexto}</span>
                  <i aria-hidden="true">
                    <ArrowRight size={16} />
                  </i>
                </a>
              )}
            </div>
            <div className="destination-photo">
              {foto && <img key={foto.url} src={foto.url} alt={foto.textoAlt} loading="lazy" decoding="async" />}
              <div className="photo-chip">
                <span>{seccion.chipMarca}</span>
                <strong>
                  {pad2(indice + 1)} / {pad2(proyectos.length)}
                </strong>
              </div>
            </div>
          </div>
          <TarjetasAlojamientos alojamientos={alojamientos} proyectoId={activo.id} titulo={TEXTOS.alojamiento.enEsteDestino} />
        </div>
      </div>
    </section>
  );
}
