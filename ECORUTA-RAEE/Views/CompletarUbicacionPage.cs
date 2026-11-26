using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class CompletarUbicacionPage : ContentPage
{
    private PuntoRecogida? _punto;
    private bool _guardando;

    internal CompletarUbicacionPage(SolicitudRecoleccion solicitud)
    {
        Title = "Completar ubicación";
        var estado = Texto("Ubicación por completar");
        var confirmar = new CheckBox();
        SemanticProperties.SetDescription(confirmar, "Confirmo que el punto corresponde a la zona registrada");
        var contenido = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        contenido.Add(Texto(Title, "PageTitle"));
        contenido.Add(Texto(SesionDemostracion.Aviso));
        contenido.Add(Tarjeta(Texto(solicitud.CodigoSolicitud, "SectionHeading"),
            Texto(solicitud.Zona), Texto(solicitud.DireccionReferencia)));
        contenido.Add(estado);
        contenido.Add(Boton("Elegir en el mapa", () => Navigation.PushModalAsync(new SelectorUbicacionPage(_punto, punto =>
        {
            _punto = punto;
            confirmar.IsChecked = false;
            estado.Text = "Ubicación seleccionada. Confirma la zona antes de guardar.";
        }))));
        contenido.Add(Texto($"Confirma que el punto corresponde a {solicitud.Zona}. La confirmación no verifica geográficamente la zona."));
        contenido.Add(confirmar);
        contenido.Add(Boton("Guardar ubicación", GuardarAsync, principal: true));
        contenido.Add(Boton("Volver", async () =>
        {
            if (!_guardando)
                await Navigation.PopModalAsync();
        }));
        Content = new ScrollView { Content = contenido };

        async Task GuardarAsync()
        {
            if (_punto is not { } punto || !confirmar.IsChecked)
            {
                estado.Text = "Selecciona un punto y confirma la zona.";
                return;
            }
            _guardando = true;
            contenido.IsEnabled = false;
            try
            {
                await OperacionBaseDatos.EjecutarAsync(async () =>
                {
                    if (await SolicitudService.Instancia.CompletarUbicacionAsync(solicitud, punto))
                        await Navigation.PopModalAsync();
                    else
                        estado.Text = "La solicitud cambió o su ubicación ya se completó. Vuelve al listado para actualizarla.";
                });
            }
            finally
            {
                contenido.IsEnabled = true;
                _guardando = false;
            }
        }
    }

    protected override bool OnBackButtonPressed() => _guardando || base.OnBackButtonPressed();
}
