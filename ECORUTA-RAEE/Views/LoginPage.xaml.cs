namespace ECORUTA_RAEE.Views;

public partial class LoginPage : ContentPage
{
	public LoginPage()
	{
		InitializeComponent();
	}

    private void IniciarSesion_Clicked(
        object sender,
        EventArgs e)
    {
        string usuario = UsuarioEntry.Text?.Trim() ?? "";
        string contrasena = ContrasenaEntry.Text ?? "";

        // Credenciales fijas para la práctica.
        if (usuario == "admin" && contrasena == "1234")
        {
            Application.Current!.Windows[0].Page =
                new AppShell();
        }
        else
        {
            MensajeError.Text =
                "Usuario o contraseña incorrectos.";

            MensajeError.IsVisible = true;
        }
    }
}