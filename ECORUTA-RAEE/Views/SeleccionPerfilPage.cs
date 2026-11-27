using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.Views;

internal sealed class SeleccionPerfilPage : ContentPage
{
    public SeleccionPerfilPage()
    {
        Title = "Elige un perfil";
        Style = TarjetasDemo.Estilo("CanvasVisual");
        var contenido = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        contenido.Add(TarjetasDemo.Encabezado("Elige un perfil", SesionDemostracion.Aviso, "panel"));
        Agregar("persona", "Ciudadano", "Registra tus equipos y solicita su recogida.", PerfilDemostracion.Ciudadano);
        Agregar("panel", "Gestor", "Organiza solicitudes, jornadas y rutas.", PerfilDemostracion.Gestor);
        Agregar("camion", "Recolector", "Consulta las jornadas y los puntos de recogida.", PerfilDemostracion.Recolector);
        contenido.Add(TarjetasDemo.Boton("Cerrar sesión", () => { NavegacionDemostracion.Salir(); return Task.CompletedTask; }));
        Content = new ScrollView { Content = contenido };

        void Agregar(string icono, string titulo, string descripcion, PerfilDemostracion perfil)
        {
            contenido.Add(TarjetasDemo.Tarjeta(TarjetasDemo.ConIcono(icono, new VerticalStackLayout
                { Spacing = 4, Children = { TarjetasDemo.Texto(titulo, "TituloVisual"), TarjetasDemo.Texto(descripcion) } }),
                TarjetasDemo.Boton($"Continuar como {perfil.ToString().ToLowerInvariant()}",
                    () => { NavegacionDemostracion.Entrar(perfil); return Task.CompletedTask; })));
        }
    }
}
