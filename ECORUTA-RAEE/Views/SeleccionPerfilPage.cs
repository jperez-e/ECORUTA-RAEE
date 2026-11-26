using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.Views;

internal sealed class SeleccionPerfilPage : ContentPage
{
    public SeleccionPerfilPage()
    {
        Title = "Elige un perfil";
        var contenido = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        contenido.Add(TarjetasDemo.Texto("Elige un perfil", "PageTitle"));
        contenido.Add(TarjetasDemo.Texto(SesionDemostracion.Aviso));
        Agregar("♻  Ciudadano", "Registra tus equipos y solicita su recogida.", PerfilDemostracion.Ciudadano);
        Agregar("▤  Gestor", "Organiza solicitudes, jornadas y rutas.", PerfilDemostracion.Gestor);
        Agregar("⌖  Recolector", "Consulta las jornadas y los puntos de recogida.", PerfilDemostracion.Recolector);
        contenido.Add(TarjetasDemo.Boton("Cerrar sesión", () => { NavegacionDemostracion.Salir(); return Task.CompletedTask; }));
        Content = new ScrollView { Content = contenido };

        void Agregar(string titulo, string descripcion, PerfilDemostracion perfil)
        {
            contenido.Add(TarjetasDemo.Tarjeta(TarjetasDemo.Texto(titulo, "SectionHeading"),
                TarjetasDemo.Texto(descripcion), TarjetasDemo.Boton($"Continuar como {perfil.ToString().ToLowerInvariant()}",
                    () => { NavegacionDemostracion.Entrar(perfil); return Task.CompletedTask; }, principal: true)));
        }
    }
}
