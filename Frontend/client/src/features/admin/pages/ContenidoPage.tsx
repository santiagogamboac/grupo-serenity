import { useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import type { EnlaceNavegacion, MediaDto, SeccionClave } from "@/lib/types";
import { useContenido, useGuardarSeccion } from "../api";
import { Campo, CampoImagen, EncabezadoPagina, type MediaRef } from "../componentes";
import { SECCIONES, type CampoSpec, type SeccionSpec } from "../contenidoSchema";

type Datos = Record<string, unknown>;

export function ContenidoPage() {
  const { data, isPending } = useContenido();
  const [clave, setClave] = useState<SeccionClave>("hero");
  const spec = SECCIONES.find(s => s.clave === clave)!;

  return (
    <>
      <EncabezadoPagina titulo="Contenido del sitio" descripcion="Todos los textos e imágenes de la página principal. Los cambios se ven al recargar el sitio." />

      <Tabs value={clave} onValueChange={v => setClave(v as SeccionClave)}>
        <TabsList className="mb-4 h-auto flex-wrap justify-start">
          {SECCIONES.map(s => (
            <TabsTrigger key={s.clave} value={s.clave}>
              {s.titulo}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      {isPending && <Skeleton className="h-96" />}
      {data && (
        // key: al cambiar de sección o tras guardar, el formulario se reinicia con los datos del servidor.
        <FormularioSeccion key={`${clave}:${JSON.stringify(data[clave])}`} spec={spec} datos={data[clave] as unknown as Datos} />
      )}
    </>
  );
}

function FormularioSeccion({ spec, datos }: { spec: SeccionSpec; datos: Datos }) {
  const guardar = useGuardarSeccion();
  const [valores, setValores] = useState<Datos>(datos);
  const sucio = JSON.stringify(valores) !== JSON.stringify(datos);

  const poner = (nombre: string, valor: unknown) => setValores(v => ({ ...v, [nombre]: valor }));

  const enviar = (e: React.FormEvent) => {
    e.preventDefault();
    const cuerpo: Datos = { ...valores };
    for (const c of spec.campos) {
      if (c.tipo !== "imagen" || !c.idCampo) continue;
      const media = valores[c.nombre] as MediaRef | MediaDto | null | undefined;
      cuerpo[c.idCampo] = media?.id ?? null;
      delete cuerpo[c.nombre]; // el servidor devuelve la imagen expandida; al guardar solo va el id
    }
    guardar.mutate({ clave: spec.clave, cuerpo });
  };

  return (
    <form onSubmit={enviar} className="space-y-5 rounded-lg border bg-card p-5" aria-label={`Editar ${spec.titulo}`}>
      <p className="text-sm text-muted-foreground">{spec.descripcion}</p>

      {spec.clave === "navegacion" && (
        <EditorEnlaces items={valores.items as EnlaceNavegacion[]} onChange={items => poner("items", items)} />
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        {spec.campos.map(c => (
          <div key={c.nombre} className={c.tipo === "largo" || c.tipo === "titular" || c.tipo === "imagen" ? "sm:col-span-2" : ""}>
            <CampoDinamico campo={c} valor={valores[c.nombre]} onChange={v => poner(c.nombre, v)} />
          </div>
        ))}
      </div>

      <div className="flex items-center gap-3 border-t pt-4">
        <Button type="submit" disabled={!sucio || guardar.isPending}>
          {guardar.isPending ? "Guardando…" : "Guardar cambios"}
        </Button>
        {!sucio && <span className="text-xs text-muted-foreground">Sin cambios por guardar.</span>}
        {sucio && (
          <Button type="button" variant="ghost" onClick={() => setValores(datos)}>
            Descartar
          </Button>
        )}
      </div>
    </form>
  );
}

function CampoDinamico({ campo, valor, onChange }: { campo: CampoSpec; valor: unknown; onChange: (v: unknown) => void }) {
  const id = `campo-${campo.nombre}`;

  switch (campo.tipo) {
    case "bool":
      return (
        <label className="flex items-center gap-3 text-sm">
          <Switch checked={Boolean(valor)} onCheckedChange={onChange} aria-label={campo.etiqueta} />
          {campo.etiqueta}
        </label>
      );
    case "imagen":
      return <CampoImagen etiqueta={campo.etiqueta} ayuda={campo.ayuda} valor={valor as MediaRef | null | undefined} onChange={onChange} />;
    case "largo":
      return (
        <Campo etiqueta={campo.etiqueta} ayuda={campo.ayuda} htmlFor={id}>
          <Textarea id={id} rows={3} value={String(valor ?? "")} onChange={e => onChange(e.target.value)} />
        </Campo>
      );
    case "titular":
      return (
        <Campo etiqueta={campo.etiqueta} ayuda={campo.ayuda} htmlFor={id}>
          <Textarea id={id} rows={2} value={String(valor ?? "")} onChange={e => onChange(e.target.value)} className="font-mono text-[13px]" />
        </Campo>
      );
    default:
      return (
        <Campo etiqueta={campo.etiqueta} ayuda={campo.ayuda} htmlFor={id}>
          <Input id={id} value={String(valor ?? "")} onChange={e => onChange(e.target.value)} placeholder={campo.tipo === "enlace" ? "https://…  o  #seccion" : undefined} />
        </Campo>
      );
  }
}

function EditorEnlaces({ items, onChange }: { items: EnlaceNavegacion[]; onChange: (items: EnlaceNavegacion[]) => void }) {
  const cambiar = (i: number, parcial: Partial<EnlaceNavegacion>) => onChange(items.map((it, n) => (n === i ? { ...it, ...parcial } : it)));

  return (
    <fieldset className="space-y-2">
      <legend className="mb-1 text-sm font-medium">Enlaces del menú</legend>
      {items.map((it, i) => (
        <div key={i} className="flex items-center gap-2">
          <Input value={it.texto} onChange={e => cambiar(i, { texto: e.target.value })} placeholder="Texto" aria-label={`Texto del enlace ${i + 1}`} />
          <Input value={it.href} onChange={e => cambiar(i, { href: e.target.value })} placeholder="#seccion" aria-label={`Destino del enlace ${i + 1}`} />
          <Button type="button" variant="ghost" size="icon-sm" aria-label={`Quitar enlace ${i + 1}`} onClick={() => onChange(items.filter((_, n) => n !== i))}>
            <Trash2 className="text-destructive" />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" disabled={items.length >= 8} onClick={() => onChange([...items, { texto: "", href: "#" }])}>
        <Plus /> Agregar enlace
      </Button>
    </fieldset>
  );
}
