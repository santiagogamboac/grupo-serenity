import { useState, type ReactNode } from "react";
import { Link, useLocation } from "wouter";
import {
  ExternalLink,
  FileText,
  Heart,
  Images,
  Inbox,
  KeyRound,
  LayoutDashboard,
  LogOut,
  MapPinned,
  Menu,
  MessageSquareQuote,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useAuth } from "./auth";
import { useDashboard } from "./api";

const ENLACES = [
  { href: "/admin", etiqueta: "Resumen", Icono: LayoutDashboard, exacto: true },
  { href: "/admin/solicitudes", etiqueta: "Solicitudes", Icono: Inbox },
  { href: "/admin/proyectos", etiqueta: "Proyectos", Icono: MapPinned },
  { href: "/admin/contenido", etiqueta: "Contenido del sitio", Icono: FileText },
  { href: "/admin/valores", etiqueta: "Valores", Icono: Heart },
  { href: "/admin/resenas", etiqueta: "Reseñas", Icono: MessageSquareQuote },
  { href: "/admin/medios", etiqueta: "Imágenes", Icono: Images },
  { href: "/admin/cuenta", etiqueta: "Mi cuenta", Icono: KeyRound },
];

export function AdminLayout({ children }: { children: ReactNode }) {
  const [ubicacion] = useLocation();
  const { usuario, logout } = useAuth();
  const [abierto, setAbierto] = useState(false);
  const { data: resumen } = useDashboard();

  const activo = (e: (typeof ENLACES)[number]) =>
    e.exacto ? ubicacion === e.href : ubicacion === e.href || ubicacion.startsWith(`${e.href}/`);

  const menu = (
    <nav className="flex flex-1 flex-col gap-1 p-3" aria-label="Panel de administración">
      {ENLACES.map(e => (
        <Link
          key={e.href}
          href={e.href}
          onClick={() => setAbierto(false)}
          className={cn(
            "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-white/70 transition hover:bg-white/10 hover:text-white",
            activo(e) && "bg-white/15 text-white"
          )}
        >
          <e.Icono className="size-4" />
          <span className="flex-1">{e.etiqueta}</span>
          {e.href === "/admin/solicitudes" && !!resumen?.solicitudesNuevas && (
            <span className="rounded-full bg-emerald-300 px-1.5 text-[11px] font-bold text-emerald-950">
              {resumen.solicitudesNuevas}
            </span>
          )}
        </Link>
      ))}
    </nav>
  );

  return (
    <div className="min-h-screen bg-background md:grid md:grid-cols-[250px_1fr]">
      {/* Barra superior en móvil */}
      <header className="flex items-center justify-between bg-[#183847] px-4 py-3 text-white md:hidden">
        <span className="font-serif text-lg">Serenity · Admin</span>
        <Button variant="ghost" size="icon" className="text-white hover:bg-white/10 hover:text-white" onClick={() => setAbierto(o => !o)} aria-label={abierto ? "Cerrar menú" : "Abrir menú"}>
          {abierto ? <X /> : <Menu />}
        </Button>
      </header>

      <aside className={cn("bg-[#183847] text-white md:sticky md:top-0 md:flex md:h-screen md:flex-col", abierto ? "flex flex-col" : "hidden")}>
        <div className="hidden px-5 pb-2 pt-6 md:block">
          <div className="font-serif text-2xl leading-none">Serenity</div>
          <div className="mt-1 text-[10px] uppercase tracking-[0.18em] text-white/50">Panel de administración</div>
        </div>
        {menu}
        <div className="space-y-2 border-t border-white/10 p-3 text-sm">
          <a href="/" target="_blank" rel="noreferrer" className="flex items-center gap-2 rounded-md px-3 py-2 text-white/70 hover:bg-white/10 hover:text-white">
            <ExternalLink className="size-4" /> Ver sitio
          </a>
          <div className="px-3 text-xs text-white/50">{usuario?.correo}</div>
          <Button variant="ghost" className="w-full justify-start text-white/80 hover:bg-white/10 hover:text-white" onClick={() => logout()}>
            <LogOut /> Cerrar sesión
          </Button>
        </div>
      </aside>

      <main className="min-w-0 p-4 sm:p-6 lg:p-8">
        <div className="mx-auto max-w-5xl">{children}</div>
      </main>
    </div>
  );
}
