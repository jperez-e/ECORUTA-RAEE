using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.Views;

internal static class NavegacionDemostracion
{
    internal static void ElegirPerfil() => Mostrar(null, new SeleccionPerfilPage());
    internal static void Entrar(PerfilDemostracion perfil) => Mostrar(perfil, new AppShell(perfil));
    internal static void Salir() => Mostrar(null, new LoginPage());

    private static void Mostrar(PerfilDemostracion? perfil, Page raiz)
    {
        SesionDemostracion.Perfil = perfil;
        Application.Current!.Windows[0].Page = raiz;
    }
}
