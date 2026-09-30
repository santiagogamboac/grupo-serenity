import { useEffect } from "react";
import { plainRich } from "@/lib/rich";
import { TEXTOS } from "@/features/public/textos";
import { useHome } from "@/features/public/useHome";
import { useScrollEffects } from "@/features/public/useScrollEffects";
import { BarraAnuncio, ColibriScroll, Encabezado } from "@/features/public/sections/Cabecera";
import { Galeria } from "@/features/public/sections/Galeria";
import { Destinos } from "@/features/public/sections/Destinos";
import { Pie } from "@/features/public/sections/Pie";
import { Experiencia, Portada } from "@/features/public/sections/Portada";
import { Reservas } from "@/features/public/sections/Reservas";
import { Resenas } from "@/features/public/sections/Resenas";
import { Valores } from "@/features/public/sections/Valores";

/** Landing pública: todo el contenido llega de GET /api/public/home. */
export default function Home() {
  const { data, isPending, isError, refetch, isFetching } = useHome();
  const { scrolled, progress } = useScrollEffects();

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

  return (
    <main className="site-shell">
      <BarraAnuncio anuncio={c.anuncio} />
      <Encabezado marca={c.marca} navegacion={c.navegacion} scrolled={scrolled} />
      <ColibriScroll marca={c.marca} progress={progress} />
      <Portada hero={c.hero} totalDestinos={proyectos.length} />
      <Experiencia intro={c.intro} />
      <Valores seccion={c.valores} valores={valores} />
      <Destinos seccion={c.destinos} proyectos={proyectos} />
      <Galeria seccion={c.destinos} proyectos={proyectos} />
      <Resenas seccion={c.resenas} resenas={resenas} />
      <Reservas seccion={c.reservas} proyectos={proyectos} />
      <Pie marca={c.marca} footer={c.footer} />
    </main>
  );
}
