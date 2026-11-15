
using System;
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();

        // Mantener el ViewModel original.
        BindingContext = new LoginViewModel();
    }

    // Al pulsar "Siguiente" en el campo usuario,
    // mover el cursor al campo contraseña.
    private void UsuarioEntry_Completed(
        object sender,
        EventArgs e)
    {
        PasswordEntry.Focus();
    }

    // Mostrar u ocultar la contraseña.
    private void AlternarVisibilidadContrasena_Clicked(
        object sender,
        EventArgs e)
    {
        PasswordEntry.IsPassword =
            !PasswordEntry.IsPassword;

        TogglePasswordButton.Text =
            PasswordEntry.IsPassword
                ? "Mostrar"
                : "Ocultar";
    }
}
