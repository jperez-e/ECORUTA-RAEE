using ECORUTA_RAEE.Services;
using Microsoft.Maui.Storage;

namespace ECORUTA_RAEE.Views;

internal static class NavegacionCuenta
{
    private const string ClaveSesion = "ecoruta.sesion.v1";

    internal static async Task<bool> RestaurarAsync()
    {
        string? token;
        try { token = await SecureStorage.Default.GetAsync(ClaveSesion); }
        catch (Exception)
        {
            SecureStorage.Default.Remove(ClaveSesion);
            return false;
        }
        if (token is null)
            return false;
        if (await Autenticacion.Instancia.RestaurarAsync(token) is null)
        {
            SecureStorage.Default.Remove(ClaveSesion);
            return false;
        }
        AbrirPortada();
        return true;
    }

    internal static async Task<bool> EntrarAsync(string correo, string clave, bool recordar)
    {
        var (identidad, token) = await Autenticacion.Instancia.EntrarAsync(correo, clave, recordar);
        if (identidad is null)
            return false;

        try
        {
            SecureStorage.Default.Remove(ClaveSesion);
            if (token is not null)
                await SecureStorage.Default.SetAsync(ClaveSesion, token);
        }
        catch (Exception)
        {
            await Autenticacion.Instancia.SalirAsync();
            throw;
        }

        AbrirPortada();
        return true;
    }

    internal static async Task SalirAsync()
    {
        await Autenticacion.Instancia.SalirAsync();
        try { SecureStorage.Default.Remove(ClaveSesion); }
        finally { MostrarLogin(); }
    }

    internal static void MostrarLogin() => Ventana.Page = new NavigationPage(new LoginPage());

    private static Window Ventana => Application.Current!.Windows[0];
    private static void AbrirPortada()
    {
        if (Autenticacion.Instancia.Actual is { } identidad)
            Ventana.Page = new AppShell(identidad.Rol);
    }
} 
