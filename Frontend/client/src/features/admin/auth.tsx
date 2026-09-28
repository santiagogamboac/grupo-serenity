import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { Redirect } from "wouter";
import { api, refrescarSesion, SESION_EXPIRADA, sesionTokens } from "@/lib/api";
import { queryClient } from "@/lib/queryClient";
import type { LoginResponse, UsuarioAdmin } from "./tipos";

type Estado = "cargando" | "anonimo" | "autenticado";

interface AuthCtx {
  estado: Estado;
  usuario: UsuarioAdmin | null;
  login: (correo: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const Ctx = createContext<AuthCtx | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<Estado>("cargando");
  const [usuario, setUsuario] = useState<UsuarioAdmin | null>(null);

  // Al abrir el panel: si hay refresh token guardado, se recupera la sesión.
  useEffect(() => {
    let vivo = true;
    (async () => {
      if (!sesionTokens.refresh) {
        if (vivo) setEstado("anonimo");
        return;
      }
      try {
        await refrescarSesion();
        const { data } = await api.get<UsuarioAdmin>("/auth/me");
        if (!vivo) return;
        setUsuario(data);
        setEstado("autenticado");
      } catch {
        sesionTokens.clear();
        if (vivo) setEstado("anonimo");
      }
    })();
    return () => {
      vivo = false;
    };
  }, []);

  // El interceptor avisa cuando ya no se pudo renovar la sesión.
  useEffect(() => {
    const alExpirar = () => {
      setUsuario(null);
      setEstado("anonimo");
      queryClient.clear();
    };
    window.addEventListener(SESION_EXPIRADA, alExpirar);
    return () => window.removeEventListener(SESION_EXPIRADA, alExpirar);
  }, []);

  const login = useCallback(async (correo: string, password: string) => {
    const { data } = await api.post<LoginResponse>("/auth/login", { correo, password });
    sesionTokens.set(data.tokens.accessToken, data.tokens.refreshToken);
    setUsuario(data.user);
    setEstado("autenticado");
  }, []);

  const logout = useCallback(async () => {
    try {
      await api.post("/auth/logout");
    } catch {
      /* si falla, igual se cierra la sesión local */
    }
    sesionTokens.clear();
    queryClient.clear();
    setUsuario(null);
    setEstado("anonimo");
  }, []);

  const valor = useMemo(() => ({ estado, usuario, login, logout }), [estado, usuario, login, logout]);
  return <Ctx.Provider value={valor}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useAuth debe usarse dentro de <AuthProvider>");
  return ctx;
}

/** Protege las rutas del panel: sin sesión → /admin/login. */
export function AuthGuard({ children }: { children: ReactNode }) {
  const { estado } = useAuth();

  if (estado === "cargando") {
    return (
      <div className="flex min-h-screen items-center justify-center text-sm text-muted-foreground">Cargando…</div>
    );
  }
  if (estado === "anonimo") return <Redirect to="/admin/login" />;
  return <>{children}</>;
}
