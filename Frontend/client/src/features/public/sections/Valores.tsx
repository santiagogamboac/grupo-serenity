import { ArrowUpRight } from "lucide-react";
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
      <div className="page-width">
        <div className="section-heading section-heading--split">
          <div>
            <div className="eyebrow">
              <span>02</span>
              <i /> {seccion.etiqueta}
            </div>
            <h2>
              <RichTitle text={seccion.titulo} />
            </h2>
          </div>
          {seccion.texto && <p>{seccion.texto}</p>}
        </div>
        <div className="value-grid">
          {valores.map((v, i) => {
            const Icono = iconoDe(v.icono);
            const numero = <div className="value-number">{pad2(i + 1)}</div>;
            const icono = <Icono size={25} strokeWidth={1.4} />;

            if (v.variante === "ancha") {
              return (
                <article key={v.id} className={CLASE_VARIANTE.ancha}>
                  {numero}
                  {icono}
                  <div>
                    <h3>{v.titulo}</h3>
                    <p>{v.descripcion}</p>
                  </div>
                  <ArrowUpRight className="value-arrow" size={22} />
                </article>
              );
            }

            return (
              <article key={v.id} className={CLASE_VARIANTE[v.variante] ?? CLASE_VARIANTE.normal}>
                {numero}
                {icono}
                <h3>{v.titulo}</h3>
                <p>{v.descripcion}</p>
                {v.variante === "alta" && <div className="value-line" />}
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}
