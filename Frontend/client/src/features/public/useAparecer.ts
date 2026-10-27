import { useEffect, useRef, useState } from "react";

/**
 * Marca con `is-visible` cada `[data-aparecer]` dentro del contenedor la primera vez que entra al viewport.
 * El CSS decide la animación (solo opacity/transform). `listo` permite engancharlo cuando el contenido ya está montado.
 */
export function useAparecer<T extends HTMLElement>(listo: boolean) {
  const ref = useRef<T>(null);

  useEffect(() => {
    const raiz = ref.current;
    if (!listo || !raiz) return;
    const items = Array.from(raiz.querySelectorAll<HTMLElement>("[data-aparecer]"));
    if (typeof IntersectionObserver === "undefined") {
      items.forEach(el => el.classList.add("is-visible"));
      return;
    }
    const obs = new IntersectionObserver(
      entradas => {
        for (const e of entradas) {
          // Con un scroll rápido un elemento puede saltar de abajo a arriba sin llegar a cruzar el viewport:
          // si ya quedó por encima, también se muestra (si no, quedaría invisible para siempre).
          if (e.isIntersecting || e.boundingClientRect.bottom < 0) {
            e.target.classList.add("is-visible");
            obs.unobserve(e.target);
          }
        }
      },
      { threshold: 0, rootMargin: "0px 0px -8% 0px" }
    );
    items.forEach(el => obs.observe(el));
    return () => obs.disconnect();
  }, [listo]);

  return ref;
}

/** Href (`#id`) de la sección que ocupa el centro de la pantalla, para resaltar el enlace activo del menú. */
export function useSeccionActiva(hrefs: string[]) {
  const clave = hrefs.join("|");
  const [activa, setActiva] = useState<string>(hrefs[0] ?? "");

  useEffect(() => {
    const secciones = clave
      .split("|")
      .filter(h => h.startsWith("#") && h.length > 1)
      .map(h => document.getElementById(decodeURIComponent(h.slice(1))))
      .filter((el): el is HTMLElement => el !== null);
    if (secciones.length === 0 || typeof IntersectionObserver === "undefined") return;

    const obs = new IntersectionObserver(
      entradas => {
        for (const e of entradas) if (e.isIntersecting) setActiva(`#${e.target.id}`);
      },
      { rootMargin: "-45% 0px -50% 0px" }
    );
    secciones.forEach(s => obs.observe(s));
    return () => obs.disconnect();
  }, [clave, setActiva]);

  return activa;
}
