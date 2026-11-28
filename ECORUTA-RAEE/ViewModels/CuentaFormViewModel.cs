using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

internal enum ModoCuenta { Inicial, Ciudadano, CrearRecolector, EditarRecolector, RestablecerRecolector }

internal partial class CuentaFormViewModel(ModoCuenta modo, int? id, Func<Task> cerrar) : ObservableObject
{
    [ObservableProperty] private string nombre = "";
    [ObservableProperty] private string correo = "";
    [ObservableProperty] private string telefono = "";
    [ObservableProperty] private string clave = "";
    [ObservableProperty] private string confirmar = "";
    [ObservableProperty] private string errorNombre = "";
    [ObservableProperty] private string errorCorreo = "";
    [ObservableProperty] private string errorTelefono = "";
    [ObservableProperty] private string errorClave = "";
    [ObservableProperty] private string errorConfirmar = "";
    [ObservableProperty] private string aviso = "";
    [ObservableProperty] private bool ocupado;

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (Ocupado)
            return;
        var datos = new DatosCuenta(Nombre, Correo, Telefono);
        var errores = Autenticacion.Validar(datos, modo == ModoCuenta.EditarRecolector ? null : Clave, Confirmar);
        ErrorNombre = modo == ModoCuenta.RestablecerRecolector ? "" : Errores("nombre");
        ErrorCorreo = modo == ModoCuenta.RestablecerRecolector ? "" : Errores("correo");
        ErrorTelefono = modo == ModoCuenta.RestablecerRecolector ? "" : Errores("telefono");
        ErrorClave = Errores("clave");
        ErrorConfirmar = Errores("confirmar");
        Aviso = "";
        if (new[] { ErrorNombre, ErrorCorreo, ErrorTelefono, ErrorClave, ErrorConfirmar }.Any(e => e.Length > 0))
            return;

        Ocupado = true;
        try
        {
            var auth = Autenticacion.Instancia;
            switch (modo)
            {
                case ModoCuenta.Inicial: await auth.AprovisionarAsync(datos, Clave); break;
                case ModoCuenta.Ciudadano: await auth.RegistrarAsync(datos, Clave); break;
                case ModoCuenta.CrearRecolector: await auth.CrearRecolectorAsync(datos, Clave); break;
                case ModoCuenta.EditarRecolector: await auth.ActualizarRecolectorAsync(id!.Value, datos); break;
                case ModoCuenta.RestablecerRecolector: await auth.RestablecerRecolectorAsync(id!.Value, Clave); break;
            }
            Clave = Confirmar = "";
            await cerrar();
        }
        catch (CorreoDuplicadoException) { ErrorCorreo = "Ya existe una cuenta con este correo."; }
        catch (UnauthorizedAccessException) { Aviso = "Tu sesión no permite guardar esta cuenta."; }
        catch (Exception)
        {
            Aviso = "No se pudo guardar la cuenta. Reintenta o vuelve al inicio para actualizar el acceso.";
        }
        finally { Ocupado = false; }

        string Errores(string campo) => errores.GetValueOrDefault(campo) ?? "";
    }
}
