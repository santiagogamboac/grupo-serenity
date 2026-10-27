import { Facebook, Instagram, Youtube } from "lucide-react";
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
      <div className="page-width footer-top" data-aparecer>
        <a className="brand-lockup brand-lockup--footer" href="#inicio" aria-label={TEXTOS.inicioAria(marca.nombre)}>
          {marca.logo && <img src={marca.logo.url} alt={marca.logoAlt || marca.nombre} loading="lazy" />}
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
                <a key={nombre} className="icon-glass" href={url} target="_blank" rel="noopener noreferrer" aria-label={nombre}>
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
      </div>
    </footer>
  );
}
