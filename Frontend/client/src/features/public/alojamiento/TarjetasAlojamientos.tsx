import { ArrowRight, Users } from "lucide-react";
import { Link } from "wouter";
import { formatoPesos } from "@/lib/reservas";
import type { AlojamientoPublico } from "@/lib/types";
import { TEXTOS } from "../textos";

const T = TEXTOS.alojamiento;

/** Alojamientos reservables de un destino, como tarjetas que llevan a su página de detalle. */
export function TarjetasAlojamientos({
  alojamientos,
  proyectoId,
  excluirId,
  titulo,
}: {
  alojamientos: AlojamientoPublico[];
  proyectoId: number;
  excluirId?: number;
  titulo?: string;
}) {
  const lista = alojamientos.filter(a => a.proyectoId === proyectoId && a.id !== excluirId);
  if (lista.length === 0) return null;

  return (
    <div className="aloj-tarjetas">
      {titulo && <h3 className="aloj-tarjetas__titulo">{titulo}</h3>}
      <ul className="aloj-tarjetas__lista">
        {lista.map(a => {
          const foto = a.imagen?.url ?? a.imagenes[0]?.url;
          return (
            <li key={a.id}>
              <Link className="aloj-tarjeta" href={`/alojamientos/${a.id}`}>
                <span className="aloj-tarjeta__foto">
                  {foto && <img src={foto} alt="" loading="lazy" decoding="async" />}
                </span>
                <span className="aloj-tarjeta__info">
                  <strong>{a.nombre}</strong>
                  <span className="aloj-tarjeta__meta">
                    <Users size={14} aria-hidden="true" /> {T.capacidad(a.capacidad)}
                  </span>
                  <span className="aloj-tarjeta__precio">
                    {T.desde} <b>{formatoPesos(a.precioNoche)}</b> {T.porNoche}
                  </span>
                </span>
                <span className="aloj-tarjeta__ir" aria-hidden="true">
                  <ArrowRight size={16} />
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
