using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class CompletarUbicacionPage : ContentPage
{
    private SeleccionUbicacion? _ubicacion;
    private bool _guardando;

    internal CompletarUbicacionPage(SolicitudRecoleccion solicitud)
    {
        Title = "Completar ubicación";
        Style = Estilo("CanvasVisual");
        var estado = Texto("Ubicación por completar");
        var contenido = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        contenido.Add(Encabezado(Title, SesionDemostracion.Aviso, "punto"));
        contenido.Add(Tarjeta(Texto(solicitud.CodigoSolicitud, "TituloVisual"),
            Texto(solicitud.Zona), Texto(solicitud.DireccionReferencia)));
        contenido.Add(estado);
        contenido.Add(Boton("Elegir en el mapa", () => OperacionBaseDatos.EjecutarAsync(async () =>
        {
            bool asignada = await SolicitudService.Instancia.TieneAsignacionActivaAsync(solicitud.Id);
            await Navigation.PushModalAsync(new SelectorUbicacionPage(
                _ubicacion?.Punto, asignada ? solicitud.Zona : _ubicacion?.Zona ?? solicitud.Zona,
                asignada ? solicitud.SectorOtraZona : _ubicacion?.Sector ?? solicitud.SectorOtraZona, ubicacion =>
                {
                    _ubicacion = ubicacion;
                    estado.Text = $"Ubicación seleccionada · {ubicacion.Zona}. Guarda para aplicar el cambio.";
                }, bloquearZona: asignada, requierePunto: true));
        })));
        contenido.Add(Boton("Guardar ubicación", GuardarAsync, principal: true));
        contenido.Add(Boton("Volver", async () =>
        {
            if (!_guardando)
                await Navigation.PopModalAsync();
        }));
        Content = new ScrollView { Content = contenido };

        async Task GuardarAsync()
        {
            if (_ubicacion is not { Punto: not null } ubicacion)
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
                    if (await SolicitudService.Instancia.CompletarUbicacionAsync(solicitud, ubicacion))
                        await Navigation.PopModalAsync();
                    else
                        estado.Text = "La solicitud cambió o pertenece a una jornada activa. Actualiza el listado. Para cambiar la zona asignada, el gestor debe cancelar primero la jornada.";
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
