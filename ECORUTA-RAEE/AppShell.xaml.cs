using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.Views;

namespace ECORUTA_RAEE;

public partial class AppShell : Shell
{
    private static bool _rutasRegistradas;
    private readonly PerfilDemostracion _perfil;
    private bool _confirmando;

    internal AppShell(PerfilDemostracion perfil)
    {
        _perfil = perfil;
        InitializeComponent();
        if (!_rutasRegistradas)
        {
            Routing.RegisterRoute(nameof(SolicitudFormPage), typeof(SolicitudFormPage));
            Routing.RegisterRoute(nameof(ResiduoFormPage), typeof(ResiduoFormPage));
            Routing.RegisterRoute(nameof(ResiduosSolicitudPage), typeof(ResiduosSolicitudPage));
            Routing.RegisterRoute(nameof(JornadaDetallePage), typeof(JornadaDetallePage));
            _rutasRegistradas = true;
        }

        var portada = new FlyoutItem { Title = perfil.ToString(), Route = "perfil" };
        portada.Items.Add(new ShellContent
        {
            Title = perfil.ToString(), Route = "PortadaDemo",
            ContentTemplate = new DataTemplate(() => new PortadaDemoPage(perfil))
        });
        Items.Insert(0, portada);
        CurrentItem = portada;
        foreach (var item in Items.Where(item => item != portada &&
            (item.Route == "inicio" || perfil != PerfilDemostracion.Gestor)).ToArray())
            Items.Remove(item);
    }

    protected override async void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);
        if (!args.CanCancel)
            return;
        if (!RutasDemostracion.Permite(_perfil, args.Target.Location.OriginalString))
        {
            args.Cancel();
            return;
        }
        if (CurrentPage is SolicitudFormPage { AbriendoMapa: true } && args.Source == ShellNavigationSource.Push)
            return;
        if (CurrentPage is not IFormularioDemostracion formulario || formulario.SalidaConfirmada)
            return;
        if (formulario.Ocupado || _confirmando)
        {
            args.Cancel();
            return;
        }
        if (!formulario.HayBorrador)
            return;

        var espera = args.GetDeferral();
        try
        {
            if (!await ConfirmarSalidaAsync())
                args.Cancel();
        }
        finally { espera.Complete(); }
    }

    private async Task<bool> ConfirmarSalidaAsync()
    {
        if (_confirmando || Navigation.ModalStack.Count > 0)
            return false;
        _confirmando = true;
        try
        {
            if (CurrentPage is not IFormularioDemostracion formulario || formulario.SalidaConfirmada)
                return true;
            if (formulario.Ocupado)
                return false;
            return !formulario.HayBorrador || await DisplayAlertAsync(
                "Cambios sin guardar", "Si sales de este formulario se perderá el borrador.", "Descartar", "Seguir editando");
        }
        finally { _confirmando = false; }
    }

    private async void CambiarPerfil_Clicked(object? sender, EventArgs e)
    {
        if (await ConfirmarSalidaAsync())
            NavegacionDemostracion.ElegirPerfil();
    }

    private async void CerrarSesion_Clicked(object? sender, EventArgs e)
    {
        if (await ConfirmarSalidaAsync() &&
            await DisplayAlertAsync("Cerrar sesión", "¿Deseas salir de EcoRuta-RAEE?", "Sí", "No"))
            NavegacionDemostracion.Salir();
    }
}
