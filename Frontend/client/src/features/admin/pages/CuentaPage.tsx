import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAuth } from "../auth";
import { useCambiarPassword } from "../api";
import { Campo, EncabezadoPagina } from "../componentes";

const esquema = z
  .object({
    passwordActual: z.string().min(1, "Escribe tu contraseña actual."),
    passwordNueva: z
      .string()
      .min(10, "Mínimo 10 caracteres.")
      .max(128)
      .refine(p => /[A-Za-z]/.test(p) && /\d/.test(p), "Debe incluir al menos una letra y un número."),
    confirmacion: z.string(),
  })
  .refine(v => v.passwordNueva === v.confirmacion, { path: ["confirmacion"], message: "No coincide con la nueva contraseña." })
  .refine(v => v.passwordNueva !== v.passwordActual, { path: ["passwordNueva"], message: "Debe ser distinta de la actual." });
type Valores = z.infer<typeof esquema>;

export function CuentaPage() {
  const { usuario } = useAuth();
  const cambiar = useCambiarPassword();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<Valores>({ resolver: zodResolver(esquema) });

  const enviar = handleSubmit(async v => {
    try {
      await cambiar.mutateAsync({ passwordActual: v.passwordActual, passwordNueva: v.passwordNueva });
      reset();
    } catch {
      /* toast de error ya mostrado */
    }
  });

  return (
    <>
      <EncabezadoPagina titulo="Mi cuenta" descripcion={`${usuario?.nombre} · ${usuario?.correo}`} />
      <form onSubmit={enviar} noValidate className="max-w-md space-y-4 rounded-lg border bg-card p-5" aria-label="Cambiar contraseña">
        <h2 className="font-semibold">Cambiar contraseña</h2>
        <p className="text-sm text-muted-foreground">Al cambiarla, las demás sesiones abiertas se cierran.</p>
        <Campo etiqueta="Contraseña actual" htmlFor="passwordActual" error={errors.passwordActual?.message}>
          <Input id="passwordActual" type="password" autoComplete="current-password" {...register("passwordActual")} />
        </Campo>
        <Campo etiqueta="Nueva contraseña" htmlFor="passwordNueva" error={errors.passwordNueva?.message} ayuda="Mínimo 10 caracteres, con letras y números.">
          <Input id="passwordNueva" type="password" autoComplete="new-password" {...register("passwordNueva")} />
        </Campo>
        <Campo etiqueta="Repite la nueva contraseña" htmlFor="confirmacion" error={errors.confirmacion?.message}>
          <Input id="confirmacion" type="password" autoComplete="new-password" {...register("confirmacion")} />
        </Campo>
        <Button type="submit" disabled={cambiar.isPending}>
          {cambiar.isPending ? "Guardando…" : "Cambiar contraseña"}
        </Button>
      </form>
    </>
  );
}
