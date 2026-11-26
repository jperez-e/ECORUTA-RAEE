using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class DetalleCampoPage : ContentPage
{
    private readonly VerticalStackLayout _contenido = new() { Padding = 16, Spacing = 9 };
    private int? _jornadaId;

    private DetalleCampoPage(string titulo)
    {
        Title = titulo;
        _contenido.Add(Texto(titulo, "PageTitle"));
        _contenido.Add(Texto(SesionDemostracion.Aviso));
        _contenido.Add(Boton("Volver", () => Navigation.PopModalAsync()));
        Content = new ScrollView { Content = _contenido };
    }

    internal static DetalleCampoPage ParaJornada(int id) => new("Detalle de recogida") { _jornadaId = id };

    internal static DetalleCampoPage ParaSolicitud(SolicitudRecoleccion solicitud, IReadOnlyList<ResiduoElectronico> equipos)
    {
        var pagina = new DetalleCampoPage("Solicitud " + solicitud.CodigoSolicitud);
        pagina.MostrarSolicitud(solicitud.CodigoSolicitud, solicitud, equipos);
        if (Coordenadas.SonValidas(solicitud))
        {
            var punto = new ParadaMapa(solicitud.Id, null, solicitud.CodigoSolicitud, solicitud.DireccionReferencia,
                solicitud.Zona, $"{solicitud.PesoTotalEstimado:0.##} kg", null, solicitud.Latitud, solicitud.Longitud);
            var datos = new MapaJornada("Ubicación de recogida", solicitud.Estado, solicitud.DireccionReferencia, 0, [punto]);
            pagina._contenido.Add(Boton("Ver ubicación", () => pagina.Navigation.PushModalAsync(new MapaJornadaPage(datos))));
        }
        else
            pagina._contenido.Add(Texto("Esta solicitud no tiene coordenadas completas."));
        return pagina;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_jornadaId is not int id)
            return;
        _jornadaId = null;
        var estado = Texto("Cargando jornada…");
        _contenido.Add(estado);
        bool ok = await OperacionBaseDatos.EjecutarAsync(async () =>
        {
            var jornada = await JornadaService.Instancia.ObtenerDetalleAsync(id);
            if (jornada is not { Estado: JornadaRecoleccion.Programada })
            {
                estado.Text = "La jornada ya no está programada.";
                return;
            }
            var vm = new PortadaDemoViewModel(PerfilDemostracion.Recolector);
            await vm.CargarEquiposAsync();
            estado.Text = $"{jornada.Zona} · {jornada.FechaProgramada:dd/MM/yyyy} · {jornada.PesoTotal:0.##} kg";
            var datos = MapaJornada.Crear(jornada);
            _contenido.Add(Texto(jornada.RutaDesactualizada ? "Orden desactualizado. Informa al gestor."
                : jornada.Ruta is null ? "El gestor aún no ha guardado un orden." : datos.Metodo));
            _contenido.Add(Boton("Ver ruta en el mapa", () => Navigation.PushModalAsync(
                new MapaJornadaPage(id, consultaDeCampo: true)), true));
            var paradas = jornada.Ruta?.Paradas ??
                jornada.Solicitudes.Select(s => new ParadaRuta(s, 0, null)).ToArray();
            foreach (var parada in paradas)
                MostrarSolicitud(parada.Titulo, parada.Solicitud, vm.Equipos(parada.Solicitud.Id).ToArray());
        });
        if (!ok)
            estado.Text = "No se pudo consultar la jornada.";
    }

    private void MostrarSolicitud(string titulo, SolicitudRecoleccion solicitud, IReadOnlyList<ResiduoElectronico> equipos)
    {
        var contenido = new List<View> { Texto(titulo, "SectionHeading"), Texto(solicitud.DireccionReferencia),
            Texto($"{solicitud.Zona} · {solicitud.Estado}"), Texto(PortadaDemoPage.Resumen(solicitud, equipos)) };
        if (equipos.Count == 0)
            contenido.Add(Texto("Sin equipos asociados."));
        foreach (var equipo in equipos)
            contenido.Add(Texto($"{equipo.Categoria} · {equipo.Descripcion} · {equipo.Cantidad} × · {equipo.PesoEstimado:0.##} kg"));
        _contenido.Add(Tarjeta(contenido.ToArray()));
    }
}
