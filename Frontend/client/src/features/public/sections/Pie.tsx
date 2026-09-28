import { ChevronRight, Facebook, Instagram, Youtube } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import type { FooterContenido, MarcaContenido } from "@/lib/types";
import { TEXTOS } from "../textos";

export function Pie({ marca, footer }: { marca: MarcaContenido; footer: FooterContenido }) {
  const redes = [
    { url: footer.facebookUrl, nombre: "Facebook", Icono: Facebook },
    { url: footer.instagramUrl, nombre: "Instagram", Icono: Instagram },
    { url: footer.youtubeUrl, nombre: "Youtube", Icono: Youtube },
  ].filter(r => r.url);

  return (
    <footer className="site-footer">
      <div className="page-width footer-top">
        <a className="brand-lockup brand-lockup--footer" href="#inicio" aria-label={TEXTOS.inicioAria(marca.nombre)}>
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
        <div className="footer-slogan">
          <RichTitle text={footer.eslogan} />
        </div>
        {redes.length > 0 && (
          <div className="footer-links">
            <span>{footer.redesTitulo}</span>
            <div>
              {redes.map(({ url, nombre, Icono }) => (
                <a key={nombre} href={url} aria-label={nombre}>
                  <Icono size={17} />
                </a>
              ))}
            </div>
          </div>
        )}
      </div>
      <div className="page-width footer-bottom">
        <span>{footer.copyright}</span>
        <span>{footer.frase}</span>
        {footer.sitioActualUrl && (
          <a href={footer.sitioActualUrl}>
            {footer.sitioActualTexto} <ChevronRight size={14} />
          </a>
        )}
      </div>
    </footer>
  );
}
