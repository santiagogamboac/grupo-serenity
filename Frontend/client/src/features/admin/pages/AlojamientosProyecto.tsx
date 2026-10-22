import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import {
  CalendarX2,
  ChevronDown,
  Copy,
  Pencil,
  Plus,
  Users,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { formatoDia, formatoPesos } from "@/lib/reservas";
import {
  useActualizarAlojamiento,
  useAlojamientos,
  useAnuncios,
  useBloqueos,
  useCrearAlojamiento,
  useCrearBloqueo,
  useCrearTarifa,
  useEliminarAlojamiento,
  useEliminarBloqueo,
  useEliminarTarifa,
} from "../api";
import {
  BotonEliminar,
  Campo,
  CampoImagen,
  ConfirmarEliminar,
  SelectNativo,
  type MediaRef,
} from "../componentes";
import type { AlojamientoAdmin } from "../tipos";

const pesos = (etiqueta: string, min = 0) =>
  z.coerce
    .number({ error: `${etiqueta}: escribe un número.` })
    .int("Sin decimales: pesos enteros.")
    .min(
      min,
      min > 0
        ? `${etiqueta} debe ser mayor que cero.`
        : `${etiqueta} no puede ser negativo.`
    )
    .max(100_000_000, "Revisa la cifra: parece demasiado alta.");

const esquema = z
  .object({
    nombre: z.string().trim().min(1, "Requerido.").max(120),
    descripcion: z.string().max(2000),
    capacidad: z.coerce.number().int().min(1, "Mínimo 1.").max(50),
    huespedesIncluidos: z.coerce.number().int().min(1, "Mínimo 1.").max(50),
    precioNoche: pesos("El precio", 1),
    recargoHuespedExtra: pesos("El recargo"),
    tarifaAseo: pesos("La tarifa de aseo"),
    estanciaMinima: z.coerce.number().int().min(1).max(60),
    anuncioId: z.string(),
    publicado: z.boolean(),
  })
  .refine(d => d.huespedesIncluidos <= d.capacidad, {
    path: ["huespedesIncluidos"],
    message: "No puede superar la capacidad.",
  });
type Entrada = z.input<typeof esquema>;
type Salida = z.output<typeof esquema>;

export function AlojamientosProyecto({ proyectoId }: { proyectoId: number }) {
  const {
    data: alojamientos,
    isPending,
    isError,
  } = useAlojamientos(proyectoId);
  const [editando, setEditando] = useState<AlojamientoAdmin | "nuevo" | null>(
    null
  );

  return (
    <section className="mt-10" aria-label="Alojamientos y reserva directa">
      <div className="mb-3 flex items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold">
            Alojamientos (reserva en línea)
          </h2>
          <p className="text-sm text-muted-foreground">
            Cada alojamiento se puede reservar y pagar desde el sitio. El precio
            puede cambiar por temporada.
          </p>
        </div>
        {editando === null && (
          <Button variant="outline" onClick={() => setEditando("nuevo")}>
            <Plus /> Agregar alojamiento
          </Button>
        )}
      </div>

      {editando !== null && (
        <FormularioAlojamiento
          key={editando === "nuevo" ? "nuevo" : editando.id}
          proyectoId={proyectoId}
          alojamiento={editando === "nuevo" ? null : editando}
          onCerrar={() => setEditando(null)}
        />
      )}

      {isPending && <Skeleton className="h-24" />}
      {isError && (
        <p className="text-sm text-destructive">
          No se pudieron cargar los alojamientos.
        </p>
      )}
      {alojamientos?.length === 0 && editando === null && (
        <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
          Este proyecto aún no tiene alojamientos: en el sitio solo se ofrece el
          formulario de consulta.
        </p>
      )}
      <ul className="space-y-3">
        {alojamientos?.map(a => (
          <FilaAlojamiento
            key={a.id}
            alojamiento={a}
            onEditar={() => setEditando(a)}
          />
        ))}
      </ul>
    </section>
  );
}

function FilaAlojamiento({
  alojamiento: a,
  onEditar,
}: {
  alojamiento: AlojamientoAdmin;
  onEditar: () => void;
}) {
  const eliminar = useEliminarAlojamiento();
  const [confirmar, setConfirmar] = useState(false);
  const [abierto, setAbierto] = useState(false);

  return (
    <li
      className="rounded-lg border bg-card"
      data-testid={`alojamiento-${a.id}`}
    >
      <div className="flex flex-wrap items-center gap-4 p-4">
        <div className="size-16 shrink-0 overflow-hidden rounded-md border bg-muted">
          {a.imagen && (
            <img src={a.imagen.url} alt="" className="size-full object-cover" />
          )}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h3 className="font-medium">{a.nombre}</h3>
            {!a.publicado && (
              <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                Oculto
              </span>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            <span className="font-medium text-foreground">
              {formatoPesos(a.precioNoche)}
            </span>{" "}
            / noche · <Users className="inline size-3.5" /> hasta {a.capacidad}{" "}
            ({a.huespedesIncluidos} incluidos) · mín. {a.estanciaMinima}{" "}
            {a.estanciaMinima === 1 ? "noche" : "noches"}
            {a.tarifaAseo > 0 && ` · aseo ${formatoPesos(a.tarifaAseo)}`}
          </p>
        </div>
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setAbierto(o => !o)}
            aria-expanded={abierto}
          >
            Temporadas y calendario{" "}
            <ChevronDown
              className={`transition ${abierto ? "rotate-180" : ""}`}
            />
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label="Editar alojamiento"
            title="Editar"
            onClick={onEditar}
          >
            <Pencil />
          </Button>
          <BotonEliminar
            onClick={() => setConfirmar(true)}
            etiqueta="Eliminar alojamiento"
          />
        </div>
      </div>

      {abierto && (
        <div className="grid gap-6 border-t p-4 lg:grid-cols-2">
          <Temporadas alojamiento={a} />
          <Bloqueos alojamientoId={a.id} />
          <EnlaceIcal url={a.urlIcal} />
        </div>
      )}

      <ConfirmarEliminar
        abierto={confirmar}
        onCerrar={() => setConfirmar(false)}
        onConfirmar={() => eliminar.mutate(a.id)}
        titulo={`¿Eliminar «${a.nombre}»?`}
        descripcion="Se borran sus temporadas y bloqueos. Si ya tiene reservas no se puede eliminar: despublícalo."
      />
    </li>
  );
}

function Temporadas({ alojamiento }: { alojamiento: AlojamientoAdmin }) {
  const crear = useCrearTarifa();
  const eliminar = useEliminarTarifa();
  const [nombre, setNombre] = useState("");
  const [desde, setDesde] = useState("");
  const [hasta, setHasta] = useState("");
  const [precio, setPrecio] = useState("");

  const agregar = () => {
    if (!nombre.trim() || !desde || !hasta || !(Number(precio) > 0)) {
      toast.error("Completa nombre, fechas y precio de la temporada.");
      return;
    }
    crear.mutate(
      {
        alojamientoId: alojamiento.id,
        datos: {
          nombre: nombre.trim(),
          desde,
          hasta,
          precioNoche: Number(precio),
        },
      },
      {
        onSuccess: () => {
          setNombre("");
          setDesde("");
          setHasta("");
          setPrecio("");
        },
      }
    );
  };

  return (
    <div>
      <h4 className="mb-1 text-sm font-semibold">Temporadas</h4>
      <p className="mb-3 text-xs text-muted-foreground">
        Precio por noche distinto entre dos fechas. La fecha final no se incluye
        (es la primera noche que vuelve al precio base).
      </p>
      <ul className="mb-3 space-y-1.5 text-sm">
        {alojamiento.tarifas.map(t => (
          <li
            key={t.id}
            className="flex items-center justify-between gap-2 rounded-md bg-muted/60 px-3 py-1.5"
          >
            <span>
              <strong className="font-medium">{t.nombre}</strong> ·{" "}
              {formatoDia(t.desde, {
                day: "numeric",
                month: "short",
                year: "numeric",
              })}{" "}
              →{" "}
              {formatoDia(t.hasta, {
                day: "numeric",
                month: "short",
                year: "numeric",
              })}
            </span>
            <span className="flex items-center gap-1">
              <span className="tabular-nums">
                {formatoPesos(t.precioNoche)}
              </span>
              <BotonEliminar
                onClick={() => eliminar.mutate(t.id)}
                etiqueta={`Eliminar temporada ${t.nombre}`}
              />
            </span>
          </li>
        ))}
        {alojamiento.tarifas.length === 0 && (
          <li className="text-xs text-muted-foreground">
            Sin temporadas: siempre se cobra el precio base.
          </li>
        )}
      </ul>
      <div className="grid grid-cols-2 gap-2">
        <Input
          aria-label="Nombre de la temporada"
          placeholder="Ej.: Fin de año"
          value={nombre}
          onChange={e => setNombre(e.target.value)}
          className="col-span-2"
        />
        <Input
          aria-label="Desde"
          type="date"
          value={desde}
          onChange={e => setDesde(e.target.value)}
        />
        <Input
          aria-label="Hasta (sin incluir)"
          type="date"
          value={hasta}
          min={desde || undefined}
          onChange={e => setHasta(e.target.value)}
        />
        <Input
          aria-label="Precio por noche"
          type="number"
          min={1}
          step={1000}
          placeholder="Precio por noche"
          value={precio}
          onChange={e => setPrecio(e.target.value)}
        />
        <Button
          type="button"
          variant="outline"
          onClick={agregar}
          disabled={crear.isPending}
        >
          <Plus /> Agregar
        </Button>
      </div>
    </div>
  );
}

function Bloqueos({ alojamientoId }: { alojamientoId: number }) {
  const { data: bloqueos } = useBloqueos(alojamientoId);
  const crear = useCrearBloqueo();
  const eliminar = useEliminarBloqueo();
  const [desde, setDesde] = useState("");
  const [hasta, setHasta] = useState("");
  const [motivo, setMotivo] = useState("");

  const agregar = () => {
    if (!desde || !hasta) {
      toast.error("Elige las fechas a bloquear.");
      return;
    }
    crear.mutate(
      { alojamientoId, datos: { desde, hasta, motivo: motivo.trim() } },
      {
        onSuccess: () => {
          setDesde("");
          setHasta("");
          setMotivo("");
        },
      }
    );
  };

  return (
    <div>
      <h4 className="mb-1 flex items-center gap-1.5 text-sm font-semibold">
        <CalendarX2 className="size-4" /> Fechas bloqueadas
      </h4>
      <p className="mb-3 text-xs text-muted-foreground">
        Noches que no se pueden reservar (mantenimiento, uso propio, reservas
        por teléfono). La fecha final es el día en que vuelve a estar libre.
      </p>
      <ul className="mb-3 space-y-1.5 text-sm">
        {bloqueos?.map(b => (
          <li
            key={b.id}
            className="flex items-center justify-between gap-2 rounded-md bg-muted/60 px-3 py-1.5"
          >
            <span>
              {formatoDia(b.desde, {
                day: "numeric",
                month: "short",
                year: "numeric",
              })}{" "}
              →{" "}
              {formatoDia(b.hasta, {
                day: "numeric",
                month: "short",
                year: "numeric",
              })}
              {b.motivo && (
                <span className="text-muted-foreground"> · {b.motivo}</span>
              )}
            </span>
            <BotonEliminar
              onClick={() => eliminar.mutate(b.id)}
              etiqueta="Quitar bloqueo"
            />
          </li>
        ))}
        {bloqueos?.length === 0 && (
          <li className="text-xs text-muted-foreground">
            No hay fechas bloqueadas a mano.
          </li>
        )}
      </ul>
      <div className="grid grid-cols-2 gap-2">
        <Input
          aria-label="Bloquear desde"
          type="date"
          value={desde}
          onChange={e => setDesde(e.target.value)}
        />
        <Input
          aria-label="Hasta (libre ese día)"
          type="date"
          value={hasta}
          min={desde || undefined}
          onChange={e => setHasta(e.target.value)}
        />
        <Input
          aria-label="Motivo"
          placeholder="Motivo (opcional)"
          value={motivo}
          onChange={e => setMotivo(e.target.value)}
        />
        <Button
          type="button"
          variant="outline"
          onClick={agregar}
          disabled={crear.isPending}
        >
          <Plus /> Bloquear
        </Button>
      </div>
    </div>
  );
}

function EnlaceIcal({ url }: { url: string }) {
  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(url);
      toast.success("Enlace copiado");
    } catch {
      toast.error("No se pudo copiar: selecciónalo y cópialo a mano.");
    }
  };

  return (
    <div className="lg:col-span-2">
      <h4 className="mb-1 text-sm font-semibold">Calendario para Airbnb</h4>
      <p className="mb-2 text-xs text-muted-foreground">
        Para que Airbnb bloquee lo que se reserva en el sitio: en Airbnb →
        Calendario → Disponibilidad → Sincronizar calendarios → Importar
        calendario, pega este enlace. Es privado: no lo compartas.
      </p>
      <div className="flex gap-2">
        <Input
          readOnly
          value={url}
          aria-label="Enlace iCal del alojamiento"
          onFocus={e => e.currentTarget.select()}
          className="font-mono text-xs"
        />
        <Button type="button" variant="outline" onClick={copiar}>
          <Copy /> Copiar
        </Button>
      </div>
    </div>
  );
}

function FormularioAlojamiento({
  proyectoId,
  alojamiento,
  onCerrar,
}: {
  proyectoId: number;
  alojamiento: AlojamientoAdmin | null;
  onCerrar: () => void;
}) {
  const crear = useCrearAlojamiento();
  const actualizar = useActualizarAlojamiento();
  const { data: anuncios } = useAnuncios(proyectoId);
  const [imagen, setImagen] = useState<MediaRef | null>(
    alojamiento?.imagen
      ? { id: alojamiento.imagen.id, url: alojamiento.imagen.url }
      : null
  );

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Salida>({
    resolver: zodResolver(esquema),
    defaultValues: alojamiento
      ? {
          ...alojamiento,
          anuncioId: alojamiento.anuncioId ? String(alojamiento.anuncioId) : "",
        }
      : {
          nombre: "",
          descripcion: "",
          capacidad: 2,
          huespedesIncluidos: 2,
          precioNoche: "",
          recargoHuespedExtra: 0,
          tarifaAseo: 0,
          estanciaMinima: 1,
          anuncioId: "",
          publicado: true,
        },
  });

  const guardar = handleSubmit(async d => {
    const datos = {
      ...d,
      anuncioId: d.anuncioId ? Number(d.anuncioId) : null,
      imagenMediaId: imagen?.id ?? null,
    };
    try {
      if (alojamiento)
        await actualizar.mutateAsync({ id: alojamiento.id, datos });
      else await crear.mutateAsync({ proyectoId, datos });
      onCerrar();
    } catch {
      // useMutacion ya muestra el error.
    }
  });

  return (
    <form
      onSubmit={guardar}
      noValidate
      className="mb-4 space-y-4 rounded-lg border bg-card p-5"
      aria-label={alojamiento ? "Editar alojamiento" : "Nuevo alojamiento"}
    >
      <div className="grid gap-4 sm:grid-cols-[1fr_auto]">
        <div className="space-y-4">
          <Campo
            etiqueta="Nombre"
            htmlFor="aloj-nombre"
            error={errors.nombre?.message}
            ayuda="Como lo verá el huésped. Ej.: Cabaña del lago"
          >
            <Input id="aloj-nombre" {...register("nombre")} />
          </Campo>
          <Campo
            etiqueta="Descripción"
            htmlFor="aloj-desc"
            error={errors.descripcion?.message}
            ayuda="Opcional: camas, vista, lo que incluye."
          >
            <Textarea id="aloj-desc" rows={3} {...register("descripcion")} />
          </Campo>
        </div>
        <CampoImagen etiqueta="Foto" valor={imagen} onChange={setImagen} />
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Campo
          etiqueta="Precio por noche (COP)"
          htmlFor="aloj-precio"
          error={errors.precioNoche?.message}
        >
          <Input
            id="aloj-precio"
            type="number"
            min={1}
            step={1000}
            inputMode="numeric"
            {...register("precioNoche")}
          />
        </Campo>
        <Campo
          etiqueta="Tarifa de aseo (COP)"
          htmlFor="aloj-aseo"
          error={errors.tarifaAseo?.message}
          ayuda="Una vez por estadía."
        >
          <Input
            id="aloj-aseo"
            type="number"
            min={0}
            step={1000}
            inputMode="numeric"
            {...register("tarifaAseo")}
          />
        </Campo>
        <Campo
          etiqueta="Estancia mínima (noches)"
          htmlFor="aloj-min"
          error={errors.estanciaMinima?.message}
        >
          <Input
            id="aloj-min"
            type="number"
            min={1}
            max={60}
            {...register("estanciaMinima")}
          />
        </Campo>
        <Campo
          etiqueta="Capacidad máxima"
          htmlFor="aloj-cap"
          error={errors.capacidad?.message}
        >
          <Input
            id="aloj-cap"
            type="number"
            min={1}
            max={50}
            {...register("capacidad")}
          />
        </Campo>
        <Campo
          etiqueta="Huéspedes incluidos en el precio"
          htmlFor="aloj-inc"
          error={errors.huespedesIncluidos?.message}
        >
          <Input
            id="aloj-inc"
            type="number"
            min={1}
            max={50}
            {...register("huespedesIncluidos")}
          />
        </Campo>
        <Campo
          etiqueta="Recargo por huésped adicional (COP / noche)"
          htmlFor="aloj-extra"
          error={errors.recargoHuespedExtra?.message}
        >
          <Input
            id="aloj-extra"
            type="number"
            min={0}
            step={1000}
            inputMode="numeric"
            {...register("recargoHuespedExtra")}
          />
        </Campo>
      </div>

      <Campo
        etiqueta="Anuncio de Airbnb vinculado (opcional)"
        htmlFor="aloj-anuncio"
        ayuda="Si este alojamiento también está en Airbnb, las fechas ocupadas allá se bloquean aquí."
      >
        <SelectNativo id="aloj-anuncio" {...register("anuncioId")}>
          <option value="">Ninguno</option>
          {anuncios?.map(a => (
            <option key={a.id} value={a.id}>
              {a.nombre} (#{a.airbnbRoomId})
            </option>
          ))}
        </SelectNativo>
      </Campo>

      <label className="flex items-center gap-3 text-sm">
        <Switch
          checked={watch("publicado")}
          onCheckedChange={v => setValue("publicado", v, { shouldDirty: true })}
          aria-label="Publicado"
        />
        Publicado (se puede reservar en el sitio)
      </label>

      <div className="flex items-center gap-3 border-t pt-4">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting
            ? "Guardando…"
            : alojamiento
              ? "Guardar alojamiento"
              : "Agregar alojamiento"}
        </Button>
        <Button type="button" variant="ghost" onClick={onCerrar}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
