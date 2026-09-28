import { useMemo } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowUpRight, Check, Send } from "lucide-react";
import { RichTitle } from "@/components/RichTitle";
import { mensajeDeError } from "@/lib/api";
import type { ProyectoPublico, ReservasContenido } from "@/lib/types";
import { TEXTOS } from "../textos";
import { useCrearSolicitud } from "../useHome";

const CORREO = /^[^@\s]+@[^@\s]+\.[^@\s]{2,}$/;
const TELEFONO = /^\+?[\d\s\-().]{7,25}$/;

export const esContactoValido = (v: string) => {
  const c = v.trim();
  return CORREO.test(c) || (TELEFONO.test(c) && (c.match(/\d/g)?.length ?? 0) >= 7);
};

/** yyyy-MM-dd en hora local del navegador. */
const hoyLocal = () => new Date().toLocaleDateString("en-CA");

const esquema = z.object({
  nombre: z.string().trim().min(2, TEXTOS.form.errorNombre).max(120),
  contacto: z.string().trim().refine(esContactoValido, TEXTOS.form.errorContacto),
  fechaLlegada: z.string().refine(f => f !== "" && f >= hoyLocal(), TEXTOS.form.errorFecha),
  huespedes: z.string(),
  destino: z.string(),
  sitioWeb: z.string().optional(),
});

type Valores = z.infer<typeof esquema>;

const SIN_DESTINO = "ninguno";

export function Reservas({ seccion, proyectos }: { seccion: ReservasContenido; proyectos: ProyectoPublico[] }) {
  const crear = useCrearSolicitud();

  const valoresIniciales = useMemo<Valores>(
    () => ({
      nombre: "",
      contacto: "",
      fechaLlegada: "",
      huespedes: "2",
      destino: proyectos[0] ? `p-${proyectos[0].id}` : SIN_DESTINO,
      sitioWeb: "",
    }),
    [proyectos]
  );

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<Valores>({ resolver: zodResolver(esquema), defaultValues: valoresIniciales });

  const enviar = handleSubmit(async v => {
    const proyectoId = v.destino.startsWith("p-") ? Number(v.destino.slice(2)) : null;

    try {
      await crear.mutateAsync({
        nombre: v.nombre.trim(),
        contacto: v.contacto.trim(),
        fechaLlegada: v.fechaLlegada,
        huespedes: Number(v.huespedes),
        proyectoId,
        destinoTexto: proyectoId === null ? TEXTOS.form.destinoIndeciso : null,
        sitioWeb: v.sitioWeb ?? "",
      });
      toast.success(seccion.exitoTitulo, { description: seccion.exitoDescripcion });
      reset(valoresIniciales);
    } catch (error) {
      toast.error(TEXTOS.form.errorEnvio, { description: mensajeDeError(error) });
    }
  });

  return (
    <section className="booking-section section-pad" id="reservas">
      <div className="page-width booking-panel">
        <div className="booking-copy">
          <div className="eyebrow eyebrow--light">
            <span>05</span>
            <i /> {seccion.etiqueta}
          </div>
          <h2>
            <RichTitle text={seccion.titulo} />
          </h2>
          <p>{seccion.texto}</p>
          {seccion.whatsappUrl && (
            <a className="whatsapp-link" href={seccion.whatsappUrl}>
              <span className="whatsapp-icon">↗</span>
              {seccion.whatsappTexto} <ArrowUpRight size={16} />
            </a>
          )}
          {seccion.nota && (
            <div className="booking-note">
              <Check size={16} /> {seccion.nota}
            </div>
          )}
        </div>

        <form className="booking-form" onSubmit={enviar} noValidate>
          <div className="form-heading">
            <span>{seccion.formTitulo}</span>
            <strong>{seccion.formSubtitulo}</strong>
          </div>

          {/* Honeypot: invisible para personas, los bots suelen rellenarlo. */}
          <div className="hp-field" aria-hidden="true">
            <label>
              No llenar
              <input type="text" tabIndex={-1} autoComplete="off" {...register("sitioWeb")} />
            </label>
          </div>

          <label>
            {TEXTOS.form.nombre}
            <input placeholder={TEXTOS.form.nombrePlaceholder} autoComplete="name" {...register("nombre")} />
            {errors.nombre && <em className="field-error">{errors.nombre.message}</em>}
          </label>
          <label>
            {TEXTOS.form.contacto}
            <input placeholder={TEXTOS.form.contactoPlaceholder} autoComplete="email" {...register("contacto")} />
            {errors.contacto && <em className="field-error">{errors.contacto.message}</em>}
          </label>
          <div className="form-row">
            <label>
              {TEXTOS.form.llegada}
              <input type="date" min={hoyLocal()} {...register("fechaLlegada")} />
              {errors.fechaLlegada && <em className="field-error">{errors.fechaLlegada.message}</em>}
            </label>
            <label>
              {TEXTOS.form.huespedes}
              <select {...register("huespedes")}>
                <option value="1">{TEXTOS.form.huesped(1)}</option>
                <option value="2">{TEXTOS.form.huesped(2)}</option>
                <option value="3">{TEXTOS.form.huesped(3)}</option>
                <option value="4">{TEXTOS.form.huespedesMas}</option>
              </select>
            </label>
          </div>
          <label>
            {TEXTOS.form.destino}
            <select {...register("destino")}>
              {proyectos.map(p => (
                <option key={p.id} value={`p-${p.id}`}>
                  {p.nombre}
                </option>
              ))}
              <option value={SIN_DESTINO}>{TEXTOS.form.destinoIndeciso}</option>
            </select>
          </label>
          <button className="button button--solid" type="submit" disabled={crear.isPending}>
            {crear.isPending ? TEXTOS.enviando : seccion.botonTexto} <Send size={16} />
          </button>
          <small>{seccion.aviso}</small>
        </form>
      </div>
    </section>
  );
}
