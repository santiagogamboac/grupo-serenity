import { useState } from "react";
import { ArrowUpRight, Clock3, MapPin } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import { iconoDe } from "@/lib/icons";
import { pad2 } from "@/lib/rich";
import type { DestinosContenido, ProyectoPublico } from "@/lib/types";
import { TEXTOS } from "../textos";

export function Destinos({ seccion, proyectos }: { seccion: DestinosContenido; proyectos: ProyectoPublico[] }) {
  const [seleccion, setSeleccion] = useState(0);

  if (proyectos.length === 0) return null;

  // Si el admin despublica proyectos mientras la página está abierta, el índice se ajusta.
  const indice = Math.min(seleccion, proyectos.length - 1);
  const activo = proyectos[indice];
  const foto = activo.imagenes[0];
  const franja = activo.imagenes[1] ?? foto;

  return (
    <section className="destinations-section section-pad" id="destinos">
      <div className="page-width">
        <div className="section-heading section-heading--destinations">
          <div>
            <div className="eyebrow eyebrow--light">
              <span>03</span>
              <i /> {seccion.etiqueta}
            </div>
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

        <div className="destination-switch" role="tablist" aria-label={TEXTOS.destinosAria}>
          {proyectos.map((p, i) => {
            const Icono = iconoDe(p.icono);
            return (
              <button
                key={p.id}
                className={i === indice ? "is-active" : ""}
                onClick={() => setSeleccion(i)}
                role="tab"
                aria-selected={i === indice}
              >
                <Icono size={17} /> {p.etiqueta} <span>{pad2(i + 1)}</span>
              </button>
            );
          })}
        </div>

        <div className="destination-feature">
          <div className="destination-copy">
            <div className="destination-label">{activo.ubicacionEtiqueta}</div>
            <h3>{activo.titulo}</h3>
            <p>{activo.descripcion}</p>
            {activo.urlReserva && (
              <a className="button button--outline-light" href={activo.urlReserva}>
                {activo.ctaTexto} <ArrowUpRight size={17} />
              </a>
            )}
            <div className="destination-meta">
              {activo.metaLugar && (
                <span>
                  <MapPin size={15} /> {activo.metaLugar}
                </span>
              )}
              {activo.metaEstancia && (
                <span>
                  <Clock3 size={15} /> {activo.metaEstancia}
                </span>
              )}
            </div>
          </div>
          <div className="destination-photo">
            {foto && <img src={foto.url} alt={foto.textoAlt} />}
            <div className="photo-chip">
              <span>{seccion.chipMarca}</span>
              <strong>
                {pad2(indice + 1)} / {pad2(proyectos.length)}
              </strong>
            </div>
          </div>
        </div>

        {franja && (
          <div className="destination-strip">
            <img src={franja.url} alt={franja.textoAlt} />
            <div>
              <span>{seccion.franjaEtiqueta}</span>
              <strong>{seccion.franjaTitulo}</strong>
            </div>
            <a href="#reservas" aria-label={TEXTOS.irAReservas}>
              <ArrowUpRight size={20} />
            </a>
          </div>
        )}
      </div>
    </section>
  );
}
