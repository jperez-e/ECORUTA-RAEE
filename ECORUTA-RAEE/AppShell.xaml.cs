
namespace ECORUTA_RAEE;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Registrar formulario de solicitudes
        Routing.RegisterRoute(
            nameof(Views.SolicitudFormPage),
            typeof(Views.SolicitudFormPage));

        // Registrar formulario de residuos electrónicos
        Routing.RegisterRoute(
            nameof(Views.ResiduoFormPage),
            typeof(Views.ResiduoFormPage));

        // Registrar pantalla de equipos asociados
        // a una solicitud de recolección
        Routing.RegisterRoute(
            nameof(Views.ResiduosSolicitudPage),
            typeof(Views.ResiduosSolicitudPage));
    }

    // Cerrar sesión
    private async void CerrarSesion_Clicked(
        object sender,
        EventArgs e)
    {
        bool confirmar = await DisplayAlertAsync(
            "Cerrar sesión",
            "¿Deseas salir de EcoRuta-RAEE?",
            "Sí",
            "No");

        if (confirmar)
        {
            Application.Current!.Windows[0].Page =
                new NavigationPage(new Views.LoginPage());
        }
    }
}
