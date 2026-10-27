import { useEffect, useMemo, useState } from "react";
import { ArrowRight, Instagram, Menu, MessageCircle, X } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import type { AnuncioContenido, MarcaContenido, NavegacionContenido } from "@/lib/types";
import { TEXTOS } from "../textos";
import { useSeccionActiva } from "../useAparecer";

export function BarraAnuncio({ anuncio }: { anuncio: AnuncioContenido }) {
  if (!anuncio.activo || !anuncio.texto) return null;
  return (
    <div className="announcement-bar">
      <span className="announcement-dot" />
      <span>{anuncio.texto}</span>
      {anuncio.ctaTexto && (
        <a href={anuncio.ctaHref}>
          {anuncio.ctaTexto} <ArrowRight size={14} />
        </a>
      )}
    </div>
  );
}

export function Encabezado({
  marca,
  navegacion,
  scrolled,
  whatsappUrl,
  instagramUrl,
}: {
  marca: MarcaContenido;
  navegacion: NavegacionContenido;
  scrolled: boolean;
  whatsappUrl?: string;
  instagramUrl?: string;
}) {
  const [menuAbierto, setMenuAbierto] = useState(false);
  const cerrar = () => setMenuAbierto(false);
  const activa = useSeccionActiva(navegacion.items.map(i => i.href));

  useEffect(() => {
    if (!menuAbierto) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setMenuAbierto(false);
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [menuAbierto]);

  return (
    <header className={`site-header ${scrolled ? "site-header--scrolled" : ""} ${menuAbierto ? "site-header--open" : ""}`}>
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
      <nav
        id="menu-principal"
        className={`primary-nav ${menuAbierto ? "primary-nav--open" : ""}`}
        aria-label={TEXTOS.navegacionPrincipal}
      >
        {navegacion.items.map(item => (
          <a
            key={`${item.href}-${item.texto}`}
            href={item.href}
            onClick={cerrar}
            className={item.href === activa ? "is-active" : undefined}
            aria-current={item.href === activa ? "location" : undefined}
          >
            {item.texto}
          </a>
        ))}
        <a className="nav-cta nav-cta--movil" href={navegacion.ctaHref} onClick={cerrar}>
          {navegacion.ctaTexto} <ArrowRight size={16} />
        </a>
      </nav>
      <div className="header-actions">
        <a className="pill-cta" href={navegacion.ctaHref}>
          <span>{navegacion.ctaTexto}</span>
          <i aria-hidden="true">
            <ArrowRight size={16} />
          </i>
        </a>
        {whatsappUrl && (
          <a className="icon-glass" href={whatsappUrl} target="_blank" rel="noopener noreferrer" aria-label={TEXTOS.whatsapp}>
            <MessageCircle size={17} />
          </a>
        )}
        {instagramUrl && (
          <a className="icon-glass" href={instagramUrl} target="_blank" rel="noopener noreferrer" aria-label={TEXTOS.instagram}>
            <Instagram size={17} />
          </a>
        )}
        <button
          type="button"
          className="mobile-menu-button icon-glass"
          aria-label={menuAbierto ? TEXTOS.menuCerrar : TEXTOS.menuAbrir}
          aria-expanded={menuAbierto}
          aria-controls="menu-principal"
          onClick={() => setMenuAbierto(o => !o)}
        >
          {menuAbierto ? <X size={20} /> : <Menu size={20} />}
        </button>
      </div>
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
