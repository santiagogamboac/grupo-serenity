import { useEffect, useMemo, useState } from "react";
import { plainRich } from "@/lib/rich";
import { TEXTOS } from "@/features/public/textos";
import { useAparecer } from "@/features/public/useAparecer";
import { useHome } from "@/features/public/useHome";
import { useScrollEffects } from "@/features/public/useScrollEffects";
import { BarraAnuncio, ColibriScroll, Encabezado } from "@/features/public/sections/Cabecera";
import { Galeria } from "@/features/public/sections/Galeria";
import { Destinos } from "@/features/public/sections/Destinos";
import { Pie } from "@/features/public/sections/Pie";
import { Experiencia, Portada } from "@/features/public/sections/Portada";
import { Reservas, type PedidoDestino } from "@/features/public/sections/Reservas";
import { Resenas } from "@/features/public/sections/Resenas";
import { Valores } from "@/features/public/sections/Valores";

/** Landing pública: todo el contenido llega de GET /api/public/home. */
export default function Home() {
  const { data, isPending, isError, refetch, isFetching } = useHome();
  const { scrolled } = useScrollEffects();
  const raiz = useAparecer<HTMLElement>(Boolean(data));
  const [pedido, setPedido] = useState<PedidoDestino>(null);
  const reservables = useMemo(() => new Set(data?.reservaDirecta.alojamientos.map(a => a.proyectoId)), [data]);

  const titulo = data?.contenido.marca.tituloSitio;
  useEffect(() => {
    if (titulo) document.title = plainRich(titulo);
  }, [titulo]);

  if (isPending) {
    return (
      <main className="home-status" aria-busy="true">
        <div className="home-status__spinner" />
        <span>{TEXTOS.cargando}</span>
      </main>
    );
  }

  if (isError) {
    return (
      <main className="home-status" role="alert">
        <strong>{TEXTOS.errorTitulo}</strong>
        <span>{TEXTOS.errorDetalle}</span>
        <button className="button button--solid" onClick={() => refetch()} disabled={isFetching}>
          {TEXTOS.reintentar}
        </button>
      </main>
    );
  }

  const { contenido: c, proyectos, valores, resenas } = data;
  const fondo = c.hero.imagenPrincipal?.url;

  return (
    <main className="site-shell site-shell--glass" ref={raiz}>
      {/* Fondo: la misma foto de portada, desenfocada, asoma alrededor del marco (efecto vidrio). */}
      <div className="site-backdrop" aria-hidden="true">
        {fondo && <img src={fondo} alt="" />}
      </div>
      <ColibriScroll marca={c.marca} />
      <div className="site-frame">
        <BarraAnuncio anuncio={c.anuncio} />
        <Portada
          hero={c.hero}
          resenas={c.resenas}
          proyectos={proyectos}
          cabecera={
            <Encabezado
              marca={c.marca}
              navegacion={c.navegacion}
              scrolled={scrolled}
              whatsappUrl={c.reservas.whatsappUrl}
              instagramUrl={c.footer.instagramUrl}
            />
          }
        />
        <Experiencia intro={c.intro} hero={c.hero} />
        <Valores seccion={c.valores} valores={valores} />
        <Destinos
          seccion={c.destinos}
          proyectos={proyectos}
          reservables={reservables}
          alojamientos={data.reservaDirecta.alojamientos}
          onReservar={id => setPedido(p => ({ proyectoId: id, n: (p?.n ?? 0) + 1 }))}
        />
        <Galeria seccion={c.destinos} proyectos={proyectos} />
        <Resenas seccion={c.resenas} resenas={resenas} />
        <Reservas seccion={c.reservas} proyectos={proyectos} reservaDirecta={data.reservaDirecta} pedido={pedido} />
        <Pie marca={c.marca} footer={c.footer} />
      </div>
    </main>
  );
}
