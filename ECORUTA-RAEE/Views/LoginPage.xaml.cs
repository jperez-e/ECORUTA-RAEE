using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm = new();
    private bool _primeraVez = true;

    public LoginPage()
    {
        InitializeComponent();
        BindingContext = _vm;
        CamposCuenta.Configurar(CorreoEntry, BordeCorreo, ErrorCorreo);
        CamposCuenta.Configurar(ClaveEntry, BordeClave, ErrorClave);
        CamposCuenta.PrepararError(Mensaje);
        _vm.PropertyChanged += (_, _) => ActualizarPantalla();
        ActualizarPantalla();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        bool restaurar = _primeraVez;
        _primeraVez = false;
        await _vm.PrepararAsync(restaurar);
    }

    private void ActualizarPantalla()
    {
        Campos.IsVisible = _vm.Listo && !_vm.RequiereGestor;
        Campos.IsEnabled = !_vm.Ocupado;
        AvisoInicial.IsVisible = _vm.Listo && _vm.RequiereGestor;
        Configurar.IsVisible = AvisoInicial.IsVisible && !_vm.Ocupado;
        Registro.IsVisible = Campos.IsVisible && !_vm.Ocupado;
        Reintentar.IsVisible = !_vm.Ocupado && !_vm.Listo && _vm.MensajeError.Length > 0;
        if (!_vm.TieneClave)
            MostrarClave(false);
    }

    private void Correo_Completed(object? sender, EventArgs e) => ClaveEntry.Focus();
    private async void Clave_Completed(object? sender, EventArgs e) => await _vm.IniciarSesionCommand.ExecuteAsync(null);
    private void Alternar_Clicked(object? sender, EventArgs e) => MostrarClave(ClaveEntry.IsPassword);
    private void MostrarClave(bool mostrar)
    {
        ClaveEntry.IsPassword = !mostrar;
        VerClave.Source = mostrar ? "cuenta_ojo_cerrado.png" : "cuenta_ojo.png";
        SemanticProperties.SetDescription(VerClave, mostrar ? "Ocultar contraseña" : "Mostrar contraseña");
    }

    private async void Registro_Clicked(object? sender, EventArgs e)
    {
        if (!_vm.Ocupado && _vm.Listo && !_vm.RequiereGestor)
            await Navigation.PushAsync(new CuentaFormPage(ModoCuenta.Ciudadano));
    }

    private async void Configurar_Clicked(object? sender, EventArgs e)
    {
        if (!_vm.Ocupado && _vm.Listo && _vm.RequiereGestor)
            await Navigation.PushAsync(new CuentaFormPage(ModoCuenta.Inicial));
    }
}
