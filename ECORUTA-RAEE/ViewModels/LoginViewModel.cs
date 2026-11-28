using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.Views;

namespace ECORUTA_RAEE.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty] private string correo = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TieneClave))] private string contrasena = "";
    [ObservableProperty] private bool recordar;
    [ObservableProperty] private bool ocupado;
    [ObservableProperty] private bool requiereGestor;
    [ObservableProperty] private bool listo;
    [ObservableProperty] private string errorCorreo = "";
    [ObservableProperty] private string errorClave = "";
    [ObservableProperty] private string mensajeError = "";

    public bool TieneClave => !string.IsNullOrEmpty(Contrasena);

    internal async Task PrepararAsync(bool restaurar)
    {
        if (Ocupado)
            return;
        Ocupado = true;
        try
        {
            if (restaurar && await NavegacionCuenta.RestaurarAsync())
                return;
            RequiereGestor = await Autenticacion.Instancia.RequiereGestorAsync();
            Listo = true;
            MensajeError = "";
        }
        catch (Exception)
        {
            Listo = false;
            MensajeError = "No se pudo preparar el acceso. Vuelve a intentarlo.";
        }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private async Task IniciarSesionAsync()
    {
        if (Ocupado || !Listo || RequiereGestor)
            return;
        ErrorCorreo = string.IsNullOrWhiteSpace(Correo) ? "Introduce tu correo electrónico." : "";
        ErrorClave = string.IsNullOrEmpty(Contrasena) ? "Introduce tu contraseña." : "";
        MensajeError = "";
        if (ErrorCorreo.Length > 0 || ErrorClave.Length > 0)
            return;
        Ocupado = true;
        try
        {
            if (!await NavegacionCuenta.EntrarAsync(Correo, Contrasena, Recordar))
                MensajeError = "No se pudo iniciar sesión. Comprueba tus datos o espera unos segundos antes de volver a intentarlo.";
            else
                Contrasena = "";
        }
        catch (Exception)
        {
            MensajeError = "No se pudo iniciar sesión. Reintenta y, si el problema continúa, desmarca Recordar sesión.";
        }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private Task ReintentarAsync() => PrepararAsync(restaurar: true);
}
