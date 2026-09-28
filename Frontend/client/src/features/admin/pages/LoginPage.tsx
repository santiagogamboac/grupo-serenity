import { useState } from "react";
import { Redirect } from "wouter";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { mensajeDeError } from "@/lib/api";
import { useAuth } from "../auth";
import { Campo } from "../componentes";

const esquema = z.object({
  correo: z.string().trim().min(1, "Escribe tu correo."),
  password: z.string().min(1, "Escribe tu contraseña."),
});
type Valores = z.infer<typeof esquema>;

export function LoginPage() {
  const { estado, login } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({ resolver: zodResolver(esquema) });

  if (estado === "autenticado") return <Redirect to="/admin" />;

  const enviar = handleSubmit(async v => {
    setError(null);
    try {
      await login(v.correo, v.password);
    } catch (e) {
      setError(mensajeDeError(e, "No pudimos iniciar sesión."));
    }
  });

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#183847] p-4">
      <form onSubmit={enviar} noValidate className="w-full max-w-sm space-y-5 rounded-xl bg-card p-8 shadow-2xl">
        <div>
          <div className="font-serif text-3xl">Serenity</div>
          <p className="mt-1 text-sm text-muted-foreground">Panel de administración</p>
        </div>

        <Campo etiqueta="Correo" htmlFor="correo" error={errors.correo?.message}>
          <Input id="correo" type="email" autoComplete="username" autoFocus {...register("correo")} />
        </Campo>
        <Campo etiqueta="Contraseña" htmlFor="password" error={errors.password?.message}>
          <Input id="password" type="password" autoComplete="current-password" {...register("password")} />
        </Campo>

        {error && (
          <p className="rounded-md bg-destructive/10 p-3 text-sm text-destructive" role="alert">
            {error}
          </p>
        )}

        <Button type="submit" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? "Entrando…" : "Entrar"}
        </Button>
      </form>
    </div>
  );
}
