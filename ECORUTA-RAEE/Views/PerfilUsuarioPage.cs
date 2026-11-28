using ECORUTA_RAEE.Services;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class PerfilUsuarioPage : ContentPage
{
    private readonly Func<Task> _salir;

    internal PerfilUsuarioPage(Func<Task> salir)
    {
        _salir = salir;
        Title = "Mi perfil";
        Style = Estilo("CanvasVisual");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var pila = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        pila.Add(Encabezado("Mi perfil", "Cuenta de esta instalación", "persona"));
        if (Autenticacion.Instancia.Actual is { } usuario)
            pila.Add(Tarjeta(Texto(usuario.Nombre, "TituloVisual"), Texto(usuario.Correo),
                Texto(usuario.Telefono ?? "Sin teléfono registrado"), EstadoVisual(usuario.Rol.ToString(), "verde")));
        else
            pila.Add(Texto("La sesión ha vencido."));
        pila.Add(Boton("Cerrar sesión", _salir));
        Content = new ScrollView { Content = pila };
    }
}
