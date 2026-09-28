import { Link } from "wouter";
import { Heart, Inbox, MapPinned, MessageSquareQuote } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useDashboard } from "../api";
import { EncabezadoPagina } from "../componentes";

export function DashboardPage() {
  const { data, isPending } = useDashboard();

  const tarjetas = data
    ? [
        { href: "/admin/solicitudes", titulo: "Solicitudes nuevas", valor: data.solicitudesNuevas, detalle: `${data.solicitudesTotal} en total`, Icono: Inbox, destacada: data.solicitudesNuevas > 0 },
        { href: "/admin/proyectos", titulo: "Proyectos publicados", valor: data.proyectosPublicados, detalle: `${data.proyectosTotal} en total`, Icono: MapPinned },
        { href: "/admin/resenas", titulo: "Reseñas publicadas", valor: data.resenasPublicadas, detalle: "visibles en el sitio", Icono: MessageSquareQuote },
        { href: "/admin/valores", titulo: "Valores activos", valor: data.valoresActivos, detalle: "“Lo que nos mueve”", Icono: Heart },
      ]
    : [];

  return (
    <>
      <EncabezadoPagina titulo="Resumen" descripcion="Estado general del sitio y de las solicitudes de reserva." />
      <div className="grid gap-4 sm:grid-cols-2">
        {isPending && Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-32" />)}
        {tarjetas.map(t => (
          <Link key={t.href} href={t.href} className="group">
            <Card className={`transition group-hover:shadow-md ${t.destacada ? "border-emerald-500/50 bg-emerald-50" : ""}`}>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">{t.titulo}</CardTitle>
                <t.Icono className="size-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-4xl font-semibold tabular-nums">{t.valor}</div>
                <p className="mt-1 text-xs text-muted-foreground">{t.detalle}</p>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </>
  );
}
