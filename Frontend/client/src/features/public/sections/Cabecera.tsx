import { useEffect, useRef, useState } from "react";
import { ArrowRight, Instagram, Menu, MessageCircle, X } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import type { AnuncioContenido, MarcaContenido, NavegacionContenido } from "@/lib/types";
import { TEXTOS } from "../textos";
import { useSeccionActiva } from "../useAparecer";
import { progresoScroll } from "../useScrollEffects";

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

/** Colibrí que acompaña el scroll. Sigue el progreso con una interpolación suave (rAF), fuera del render de React. */
export function ColibriScroll({ marca }: { marca: MarcaContenido }) {
  const ref = useRef<HTMLDivElement>(null);
  const hayPajaro = Boolean(marca.pajaro);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const suavizado = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 1 : 0.1;
    let raf = 0;
    let objetivo = progresoScroll();
    let actual = objetivo;
    // El progreso se lee dentro del cuadro (rAF), no en el evento de scroll: así no fuerza un layout extra.
    const pintar = () => {
      objetivo = progresoScroll();
      actual += (objetivo - actual) * suavizado;
      if (Math.abs(objetivo - actual) < 0.0004) actual = objetivo;
      el.style.transform = `translate3d(${(Math.sin(actual * Math.PI * 4) * 12).toFixed(2)}px, ${(Math.cos(actual * Math.PI * 5) * 10).toFixed(2)}px, 0) rotate(${(Math.sin(actual * Math.PI * 3) * 8).toFixed(2)}deg)`;
      raf = actual === objetivo ? 0 : requestAnimationFrame(pintar);
    };
    const onScroll = () => {
      if (!raf) raf = requestAnimationFrame(pintar);
    };
    pintar();
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
    };
  }, [hayPajaro]);

  if (!marca.pajaro) return null;
  return (
    <div className="scroll-bird" ref={ref} aria-hidden="true">
      <span className="bird-glow" />
      <img src={marca.pajaro.url} alt="" />
    </div>
  );
}
