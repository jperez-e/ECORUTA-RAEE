using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string usuario = string.Empty;

    [ObservableProperty]
    private string contrasena = string.Empty;

    [ObservableProperty]
    private string mensajeError = string.Empty;

    [ObservableProperty]
    private bool hayError;

    [RelayCommand]
    private void IniciarSesion()
    {
        HayError = false;
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(Usuario))
        {
            MostrarError("Introduce tu nombre de usuario.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Contrasena))
        {
            MostrarError("Introduce tu contraseña.");
            return;
        }

        if (Usuario.Trim() == "admin" &&
            Contrasena == "1234")
        {
            // Navegar al área principal.
            Application.Current!.Windows[0].Page =
                new AppShell();

            return;
        }

        MostrarError("El usuario o la contraseña son incorrectos.");
    }

    private void MostrarError(string mensaje)
    {
        MensajeError = mensaje;
        HayError = true;
    }
}
