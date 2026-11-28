using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class CuentaFormPage : ContentPage
{
    private readonly CuentaFormViewModel _vm;

    internal CuentaFormPage(ModoCuenta modo, IdentidadUsuario? cuenta = null)
    {
        Title = modo switch
        {
            ModoCuenta.Inicial => "Configurar cuenta gestora",
            ModoCuenta.Ciudadano => "Crear cuenta ciudadana",
            ModoCuenta.CrearRecolector => "Nuevo recolector",
            ModoCuenta.EditarRecolector => "Editar recolector",
            _ => "Restablecer contraseña"
        };
        Style = Estilo("CanvasVisual");
        bool esPublica = modo is ModoCuenta.Inicial or ModoCuenta.Ciudadano;
        _vm = new CuentaFormViewModel(modo, cuenta?.Id, async () =>
        {
            if (esPublica)
            {
                await DisplayAlertAsync("Cuenta creada", "Ya puedes iniciar sesión con tu correo.", "Entendido");
                if (modo == ModoCuenta.Inicial)
                    NavegacionCuenta.MostrarLogin();
                else
                    await Navigation.PopAsync();
            }
            else
            {
                await Navigation.PopModalAsync();
            }
        });
        BindingContext = _vm;
        var pila = new VerticalStackLayout { Padding = 16, Spacing = 10 };
        var campos = new VerticalStackLayout { Spacing = 9 };
        if (modo == ModoCuenta.Inicial)
            campos.Add(Texto("Configura esta cuenta solo si administras el dispositivo. La instalación es local y no verifica tu identidad."));
        if (modo != ModoCuenta.RestablecerRecolector)
        {
            _vm.Nombre = cuenta?.Nombre ?? "";
            _vm.Correo = cuenta?.Correo ?? "";
            _vm.Telefono = cuenta?.Telefono ?? "";
            Agregar("Nombre completo", nameof(_vm.Nombre), nameof(_vm.ErrorNombre), Keyboard.Default, 120);
            Agregar("Correo electrónico", nameof(_vm.Correo), nameof(_vm.ErrorCorreo), Keyboard.Email, 254);
            Agregar("Teléfono opcional · con prefijo internacional", nameof(_vm.Telefono), nameof(_vm.ErrorTelefono), Keyboard.Telephone, 35);
        }
        if (modo != ModoCuenta.EditarRecolector)
        {
            Agregar("Contraseña · mínimo 12 caracteres", nameof(_vm.Clave), nameof(_vm.ErrorClave), Keyboard.Default, 128, true);
            Agregar("Confirmar contraseña", nameof(_vm.Confirmar), nameof(_vm.ErrorConfirmar), Keyboard.Default, 128, true);
        }
        if (modo == ModoCuenta.RestablecerRecolector)
            campos.Add(Texto("La contraseña nueva cerrará las sesiones recordadas de esta cuenta. Compártela con el recolector de forma privada."));

        var guardar = new Button { Text = "Guardar cuenta", Style = Estilo("PrincipalVisual"), Command = _vm.GuardarCommand };
        campos.Add(guardar);
        campos.Add(Boton("Volver", async () =>
        {
            if (_vm.Ocupado)
                return;
            if (esPublica) await Navigation.PopAsync();
            else await Navigation.PopModalAsync();
        }));
        var aviso = Texto("");
        CamposCuenta.PrepararError(aviso);
        aviso.SetBinding(Label.TextProperty, nameof(_vm.Aviso));
        var carga = new ActivityIndicator();
        carga.SetBinding(ActivityIndicator.IsRunningProperty, nameof(_vm.Ocupado));
        carga.SetBinding(IsVisibleProperty, nameof(_vm.Ocupado));
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.Ocupado))
                campos.IsEnabled = !_vm.Ocupado;
        };
        pila.Add(Encabezado(Title, "Datos de esta instalación", "persona"));
        pila.Add(carga);
        pila.Add(aviso);
        pila.Add(Tarjeta(campos));
        Content = new ScrollView { Content = pila };

        void Agregar(string etiqueta, string propiedad, string errorPropiedad, Keyboard teclado, int limite, bool secreta = false)
        {
            campos.Add(Texto(etiqueta));
            var campo = new Entry { Keyboard = teclado, MaxLength = limite, IsPassword = secreta };
            SemanticProperties.SetDescription(campo, etiqueta);
            campo.SetBinding(Entry.TextProperty, propiedad, BindingMode.TwoWay);
            var borde = new Border { Content = campo, Padding = new Thickness(10, 0),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 } };
            var error = new Label();
            error.SetBinding(Label.TextProperty, errorPropiedad);
            CamposCuenta.Configurar(campo, borde, error);
            campos.Add(borde);
            campos.Add(error);
        }
    }

    protected override bool OnBackButtonPressed() => _vm.Ocupado || base.OnBackButtonPressed();
}
