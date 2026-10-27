import { RichTitle } from "@/components/RichTitle";
import { iconoDe } from "@/lib/icons";
import { pad2 } from "@/lib/rich";
import type { ValoresContenido, ValorPublico } from "@/lib/types";

const CLASE_VARIANTE: Record<ValorPublico["variante"], string> = {
  normal: "value-card",
  alta: "value-card value-card--tall",
  ancha: "value-card value-card--wide",
};

export function Valores({ seccion, valores }: { seccion: ValoresContenido; valores: ValorPublico[] }) {
  return (
    <section className="values-section section-pad">
      <div className="glow-orb glow-orb--teal" aria-hidden="true" />
      <div className="page-width">
        <div className="section-heading section-heading--split" data-aparecer>
          <div>
            <div className="eyebrow">{seccion.etiqueta}</div>
            <h2>
              <RichTitle text={seccion.titulo} />
            </h2>
          </div>
          {seccion.texto && <p>{seccion.texto}</p>}
        </div>
        {valores.length > 0 && (
          <div className="value-grid cascada" data-aparecer data-count={Math.min(valores.length, 4)}>
            {valores.map((v, i) => {
              const Icono = iconoDe(v.icono);
              return (
                <article key={v.id} className={CLASE_VARIANTE[v.variante] ?? CLASE_VARIANTE.normal}>
                  <span className="value-icon">
                    <Icono size={24} strokeWidth={1.5} aria-hidden="true" />
                  </span>
                  <h3>{v.titulo}</h3>
                  <p>{v.descripcion}</p>
                  <span className="value-number" aria-hidden="true">
                    {pad2(i + 1)}
                  </span>
                </article>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}
