import { useMemo, useState } from "react";
import { ArrowUpRight, Menu, X } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import type { AnuncioContenido, MarcaContenido, NavegacionContenido } from "@/lib/types";
import { TEXTOS } from "../textos";

export function BarraAnuncio({ anuncio }: { anuncio: AnuncioContenido }) {
  if (!anuncio.activo || !anuncio.texto) return null;
  return (
    <div className="announcement-bar">
      <span className="announcement-dot" />
      <span>{anuncio.texto}</span>
      {anuncio.ctaTexto && (
        <a href={anuncio.ctaHref}>
          {anuncio.ctaTexto} <ArrowUpRight size={14} />
        </a>
      )}
    </div>
  );
}

export function Encabezado({
  marca,
  navegacion,
  scrolled,
}: {
  marca: MarcaContenido;
  navegacion: NavegacionContenido;
  scrolled: boolean;
}) {
  const [menuAbierto, setMenuAbierto] = useState(false);
  const cerrar = () => setMenuAbierto(false);

  return (
    <header className={`site-header ${scrolled ? "site-header--scrolled" : ""}`}>
      <a className="brand-lockup" href="#inicio" aria-label={TEXTOS.inicioAria(marca.nombre)} onClick={cerrar}>
        {marca.logo && <img src={marca.logo.url} alt={marca.logoAlt || marca.nombre} />}
        {marca.leyenda && (
          <>
            <span className="brand-divider" />
            <span className="brand-caption">
              <RichTitle text={marca.leyenda} />
            </span>
          </>
        )}
      </a>
      <button
        className="mobile-menu-button"
        aria-label={menuAbierto ? TEXTOS.menuCerrar : TEXTOS.menuAbrir}
        onClick={() => setMenuAbierto(o => !o)}
      >
        {menuAbierto ? <X size={22} /> : <Menu size={22} />}
      </button>
      <nav className={`primary-nav ${menuAbierto ? "primary-nav--open" : ""}`} aria-label={TEXTOS.navegacionPrincipal}>
        {navegacion.items.map(item => (
          <a key={`${item.href}-${item.texto}`} href={item.href} onClick={cerrar}>
            {item.texto}
          </a>
        ))}
        <a className="nav-cta" href={navegacion.ctaHref} onClick={cerrar}>
          {navegacion.ctaTexto} <ArrowUpRight size={15} />
        </a>
      </nav>
    </header>
  );
}

export function ColibriScroll({ marca, progress }: { marca: MarcaContenido; progress: number }) {
  const estilo = useMemo(
    () => ({
      transform: `translate3d(${Math.sin(progress * Math.PI * 4) * 12}px, ${Math.cos(progress * Math.PI * 5) * 10}px, 0) rotate(${Math.sin(progress * Math.PI * 3) * 8}deg)`,
    }),
    [progress]
  );

  if (!marca.pajaro) return null;
  return (
    <div className="scroll-bird" style={estilo} aria-hidden="true">
      <span className="bird-glow" />
      <img src={marca.pajaro.url} alt="" />
    </div>
  );
}
