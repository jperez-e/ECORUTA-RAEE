namespace ECORUTA_RAEE;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(
           nameof(Views.SolicitudFormPage),
           typeof(Views.SolicitudFormPage));
    }

    private async void CerrarSesion_Clicked(
       object sender,
       EventArgs e)
    {
        bool confirmar = await DisplayAlertAsync(
                "Cerrar sesión",
                "¿Deseas salir de EcoRuta RAEE?",
                "Sí",
                "No");

        if (confirmar)
        {
            Application.Current!.Windows[0].Page =
                new NavigationPage(new Views.LoginPage());
        }
    }
}
