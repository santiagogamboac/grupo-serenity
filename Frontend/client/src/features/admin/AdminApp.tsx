import { Route, Switch } from "wouter";
import NotFound from "@/pages/NotFound";
import { AuthGuard, AuthProvider } from "./auth";
import { AdminLayout } from "./AdminLayout";
import { CuentaPage } from "./pages/CuentaPage";
import { ContenidoPage } from "./pages/ContenidoPage";
import { DashboardPage } from "./pages/DashboardPage";
import { LoginPage } from "./pages/LoginPage";
import { MediaPage } from "./pages/MediaPage";
import { ProyectoFormPage } from "./pages/ProyectoFormPage";
import { ProyectosPage } from "./pages/ProyectosPage";
import { ResenasPage } from "./pages/ResenasPage";
import { ReservasPage } from "./pages/ReservasPage";
import { SolicitudesPage } from "./pages/SolicitudesPage";
import { ValoresPage } from "./pages/ValoresPage";

/** Panel de administración (todo lo que cuelga de /admin). */
export default function AdminApp() {
  return (
    <AuthProvider>
      <Switch>
        <Route path="/admin/login" component={LoginPage} />
        <Route>
          <AuthGuard>
            <AdminLayout>
              <Switch>
                <Route path="/admin" component={DashboardPage} />
                <Route path="/admin/proyectos" component={ProyectosPage} />
                <Route path="/admin/proyectos/nuevo">{() => <ProyectoFormPage id={null} />}</Route>
                <Route path="/admin/proyectos/:id">{p => <ProyectoFormPage id={Number(p.id)} />}</Route>
                <Route path="/admin/resenas" component={ResenasPage} />
                <Route path="/admin/valores" component={ValoresPage} />
                <Route path="/admin/contenido" component={ContenidoPage} />
                <Route path="/admin/reservas" component={ReservasPage} />
                <Route path="/admin/solicitudes" component={SolicitudesPage} />
                <Route path="/admin/medios" component={MediaPage} />
                <Route path="/admin/cuenta" component={CuentaPage} />
                <Route component={NotFound} />
              </Switch>
            </AdminLayout>
          </AuthGuard>
        </Route>
      </Switch>
    </AuthProvider>
  );
}
