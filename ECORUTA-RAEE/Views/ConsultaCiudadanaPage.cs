using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class ConsultaCiudadanaPage : ContentPage
{
    private readonly int? _id;
    private readonly bool _confirmacion;
    private readonly VerticalStackLayout _contenido = new() { Padding = 16, Spacing = 10 };
    private readonly Label _aviso = Texto("Cargando solicitudes…");
    private bool _cargando;
    private readonly bool _esGestion;

    internal ConsultaCiudadanaPage(int solicitudId) : this()
    {
        _id = solicitudId;
        _esGestion = true;
        Title = "Detalle de solicitud";
    }

    internal ConsultaCiudadanaPage(SolicitudCiudadana? fila = null, bool confirmacion = false)
    {
        _id = fila?.Solicitud.Id;
        _confirmacion = confirmacion;
        Title = confirmacion ? "Solicitud registrada" : fila is null ? "Mis solicitudes" : "Detalle de solicitud";
        Style = Estilo("CanvasVisual");
        Content = new ScrollView { Content = _contenido };
        _contenido.Add(_aviso);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_cargando)
            return;
        _cargando = true;
        try
        {
            if (!await OperacionBaseDatos.EjecutarAsync(async () =>
                Mostrar(await SolicitudService.Instancia.ObtenerVistaCiudadanaAsync(_id))))
            {
                _aviso.Text = "No se pudieron actualizar los datos. Vuelve a abrir la consulta para reintentar.";
                if (!_contenido.Children.Contains(_aviso))
                    _contenido.Insert(0, _aviso);
            }
        }
        finally { _cargando = false; }
    }

    private void Mostrar(IReadOnlyList<SolicitudCiudadana> filas)
    {
        _contenido.Clear();
        _contenido.Add(Boton(_confirmacion ? "Volver al inicio" : "Volver", () => Navigation.PopModalAsync()));
        if (_id is null)
        {
            _contenido.Add(Cabecera("Mis solicitudes", "Consulta el estado de tus solicitudes."));
            foreach (var fila in filas)
                _contenido.Add(TarjetaSolicitud(fila, Navigation));
        }
        else if (filas.FirstOrDefault() is { } fila)
        {
            var solicitud = fila.Solicitud;
            _contenido.Add(Cabecera(_confirmacion ? "¡Solicitud registrada!" : solicitud.CodigoSolicitud,
                $"{solicitud.CodigoSolicitud} · {fila.ZonaConSector}"));
            _contenido.Add(Tarjeta(Texto(PortadaDemoPage.Resumen(solicitud, fila.Equipos), "TituloVisual"),
                EstadoVisual(fila.EstadoVisual, Tono(fila)), Texto(fila.ProximoPaso)));
            _contenido.Add(Tarjeta(ConIcono("listo", Texto("Solicitud registrada")),
                ConIcono(solicitud.UbicacionPendiente ? "punto" : "listo",
                    Texto(solicitud.UbicacionPendiente ? "Ubicación por completar" : "Punto de recogida confirmado")),
                ConIcono("calendario", Texto(fila.FechaProgramada is { } fecha
                    ? $"Jornada programada · {fecha:d 'de' MMMM 'de' yyyy}"
                    : "Sin jornada programada"))));
            if (!_esGestion && fila.PuedeCompletarPunto)
                _contenido.Add(Boton("Completar ubicación", () => Navigation.PushModalAsync(new CompletarUbicacionPage(solicitud.Id)), true));
            if (_confirmacion)
            {
                _contenido.Add(Boton("Ver solicitud", () => Navigation.PushModalAsync(new ConsultaCiudadanaPage(fila)), true));
                return;
            }
            _contenido.Add(Tarjeta(Texto("Contacto", "TituloVisual"),
                Texto(solicitud.NombreContacto ?? "Sin nombre registrado"),
                Texto(solicitud.TelefonoContacto ?? "Sin teléfono registrado")));
            _contenido.Add(Tarjeta(Texto("Ubicación", "TituloVisual"), Texto(fila.ZonaConSector),
                Texto(solicitud.DireccionReferencia)));
            var equipos = new List<View> { Texto("Equipos electrónicos", "TituloVisual") };
            foreach (var equipo in fila.Equipos)
                equipos.Add(Texto($"{equipo.Categoria} · {equipo.Cantidad} {(equipo.Cantidad == 1 ? "unidad" : "unidades")} · {equipo.PesoEstimado:0.##} kg"));
            if (fila.Equipos.Count == 0)
                equipos.Add(Texto("Sin equipos asociados."));
            _contenido.Add(Tarjeta(equipos.ToArray()));
            if (Coordenadas.SonValidas(solicitud))
            {
                _contenido.Add(Boton("Ver ubicación", () => Navigation.PushModalAsync(MapaJornadaPage.ParaSolicitud(solicitud.Id))));
            }
        }
        if (filas.Count == 0)
            _contenido.Add(Tarjeta(Texto(_id.HasValue ? "La solicitud ya no está disponible." : "Todavía no hay solicitudes registradas.")));
    }

    private static string Tono(SolicitudCiudadana fila) => fila.FechaProgramada.HasValue ? "azul"
        : fila.Solicitud.UbicacionPendiente ? "ambar"
        : ElegibilidadRecogida.ZonaAdmitida(fila.Solicitud.Zona) ? "verde" : "gris";

    internal static Border TarjetaSolicitud(SolicitudCiudadana fila, INavigation navegacion)
    {
        var solicitud = fila.Solicitud;
        var contenido = new List<View>
        {
            Texto(solicitud.CodigoSolicitud, "TituloVisual"), EstadoVisual(fila.EstadoVisual, Tono(fila)),
            Texto(fila.ZonaConSector), Texto(PortadaDemoPage.Resumen(solicitud, fila.Equipos))
        };
        if (fila.FechaProgramada is { } fecha)
            contenido.Add(ConIcono("calendario", EstadoVisual($"Fecha programada · {fecha:d 'de' MMMM 'de' yyyy}", "azul")));
        contenido.Add(Texto(fila.ProximoPaso));
        contenido.Add(Boton("Ver detalles", () => navegacion.PushModalAsync(new ConsultaCiudadanaPage(fila))));
        if (fila.PuedeCompletarPunto)
            contenido.Add(Boton("Completar ubicación", () => navegacion.PushModalAsync(new CompletarUbicacionPage(solicitud.Id))));
        return Tarjeta(contenido.ToArray());
    }
}
