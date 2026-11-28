using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class RecolectoresPage : ContentPage
{
    private readonly VerticalStackLayout _lista = new() { Spacing = 10 };
    private bool _cargando;

    internal RecolectoresPage()
    {
        Title = "Recolectores";
        Style = Estilo("CanvasVisual");
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 10,
            Children = { Encabezado("Recolectores", "Administra las cuentas de trabajo de campo.", "camion"),
                Boton("Nuevo recolector", () => Navigation.PushModalAsync(new CuentaFormPage(ModoCuenta.CrearRecolector)), true),
                _lista } } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        if (_cargando)
            return;
        _cargando = true;
        try
        {
            await OperacionBaseDatos.EjecutarAsync(async () =>
            {
                var cuentas = await Autenticacion.Instancia.RecolectoresAsync();
                _lista.Clear();
                if (cuentas.Count == 0)
                    _lista.Add(Tarjeta(Texto("Todavía no hay recolectores registrados.")));
                foreach (var cuenta in cuentas)
                    _lista.Add(Tarjeta(Texto(cuenta.Nombre, "TituloVisual"), Texto(cuenta.Correo),
                        Texto(cuenta.Telefono ?? "Sin teléfono registrado"),
                        EstadoVisual(cuenta.Activo ? "Cuenta activa" : "Cuenta desactivada", cuenta.Activo ? "verde" : "gris"),
                        Boton("Editar datos", () => Navigation.PushModalAsync(new CuentaFormPage(ModoCuenta.EditarRecolector, cuenta))),
                        Boton("Restablecer contraseña", () => Navigation.PushModalAsync(new CuentaFormPage(ModoCuenta.RestablecerRecolector, cuenta))),
                        Boton(cuenta.Activo ? "Desactivar cuenta" : "Activar cuenta", async () =>
                        {
                            if (!await DisplayAlertAsync(cuenta.Activo ? "Desactivar cuenta" : "Activar cuenta",
                                cuenta.Activo ? "Se revocarán las sesiones recordadas de la cuenta." : "Podrá volver a iniciar sesión.",
                                "Confirmar", "Volver"))
                                return;
                            if (await OperacionBaseDatos.EjecutarAsync(() =>
                                Autenticacion.Instancia.EstadoRecolectorAsync(cuenta.Id, !cuenta.Activo)))
                                await CargarAsync();
                        })));
            });
        }
        finally { _cargando = false; }
    }
}
