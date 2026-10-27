import { useEffect, useState } from "react";

/**
 * Bandera de "página desplazada" para el header. Solo cambia al cruzar el umbral: el scroll no vuelve a renderizar
 * la landing en cada cuadro (los efectos continuos, como el colibrí, escriben el estilo directo en el DOM).
 */
export function useScrollEffects() {
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    let raf = 0;
    const medir = () => {
      raf = 0;
      setScrolled(window.scrollY > 32);
    };
    const onScroll = () => {
      if (!raf) raf = requestAnimationFrame(medir);
    };
    medir();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener("scroll", onScroll);
    };
  }, []);

  return { scrolled };
}

/** Progreso de scroll de la página (0..1). */
export function progresoScroll() {
  const max = document.documentElement.scrollHeight - window.innerHeight;
  return max > 0 ? Math.min(Math.max(window.scrollY / max, 0), 1) : 0;
}
