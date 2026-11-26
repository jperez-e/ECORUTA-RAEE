using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class PortadaDemoPage : ContentPage
{
    private readonly PerfilDemostracion _perfil;
    private readonly PortadaDemoViewModel _vm;
    private readonly VerticalStackLayout _contenido = new() { Padding = 16, Spacing = 10 };
    private bool _cargando;

    internal PortadaDemoPage(PerfilDemostracion perfil)
    {
        _perfil = perfil;
        BindingContext = _vm = new(perfil);
        Title = perfil.ToString();
        Content = new ScrollView { Content = _contenido };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_cargando)
            return;
        _cargando = true;
        try
        {
            _contenido.Clear();
            _contenido.Add(Texto(_perfil switch
            {
                PerfilDemostracion.Ciudadano => "Solicita la recogida de tus equipos",
                PerfilDemostracion.Gestor => "Panel de gestión",
                _ => "Jornadas programadas disponibles"
            }, "PageTitle"));
            _contenido.Add(Texto(SesionDemostracion.Aviso));
            var estado = Texto("Cargando…");
            _contenido.Add(estado);
            if (!await _vm.CargarAsync())
            {
                estado.Text = "No se pudieron consultar los datos.";
                return;
            }
            _contenido.Remove(estado);
            switch (_perfil)
            {
                case PerfilDemostracion.Ciudadano: MostrarSolicitudes(); break;
                case PerfilDemostracion.Gestor: MostrarPanel(); break;
                case PerfilDemostracion.Recolector: MostrarJornadas(); break;
            }
        }
        finally { _cargando = false; }
    }

    private void MostrarSolicitudes()
    {
        _contenido.Add(Boton("Nueva solicitud", () => Shell.Current.GoToAsync(nameof(SolicitudFormPage)), true));
        _contenido.Add(Texto("Solicitudes de este dispositivo", "SectionHeading"));
        if (_vm.Solicitudes.Count == 0)
            _contenido.Add(Texto("Todavía no hay solicitudes registradas."));
        foreach (var solicitud in _vm.Solicitudes)
        {
            var equipos = _vm.Equipos(solicitud.Id).ToArray();
            var tarjeta = new List<View> { Texto($"{solicitud.CodigoSolicitud} · {solicitud.Estado}", "SectionHeading"),
                Texto(solicitud.Zona), Texto(solicitud.DireccionReferencia),
                Texto(Resumen(solicitud, equipos)),
                Boton("Consultar equipos y ubicación", () => Navigation.PushModalAsync(
                    DetalleCampoPage.ParaSolicitud(solicitud, equipos))) };
            if (solicitud.UbicacionPendiente)
            {
                tarjeta.Add(Texto("Ubicación por completar. La solicitud está registrada y todavía no es elegible para una jornada automática."));
                if (solicitud.Estado == ElegibilidadRecogida.Pendiente)
                    tarjeta.Add(Boton("Completar ubicación", () => Navigation.PushModalAsync(new CompletarUbicacionPage(solicitud))));
            }
            _contenido.Add(Tarjeta(tarjeta.ToArray()));
        }
    }

    private void MostrarPanel()
    {
        if (_vm.Panel is not { } panel)
            return;
        var metricas = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) }, ColumnSpacing = 8, RowSpacing = 8 };
        metricas.Add(Metrica("Solicitudes pendientes", panel.Indicadores.Pendientes.ToString(), "#9A4F04"), 0, 0);
        metricas.Add(Metrica("Zonas listas", panel.Alertas.Count(a => a.EsLista).ToString(), "#176B3A"), 1, 0);
        metricas.Add(Metrica("Jornadas programadas", panel.Jornadas.Programadas.ToString(), "#22548B"), 0, 1);
        metricas.Add(Metrica("Peso vigente registrado", $"{panel.Indicadores.PesoVigente:0.##} kg", "#694692"), 1, 1);
        _contenido.Add(metricas);
        var proximas = panel.Alertas.Where(a => a.Nivel == NivelAlertaZona.Proxima).ToArray();
        _contenido.Add(Texto($"Zonas próximas al umbral · {proximas.Length}", "SectionHeading"));
        if (proximas.Length == 0)
            _contenido.Add(Texto("No hay zonas próximas al umbral."));
        foreach (var zona in proximas)
            _contenido.Add(Tarjeta(Texto(zona.Zona, "SectionHeading"), Texto(zona.DetalleAlerta)));
        _contenido.Add(Texto($"Ubicación por completar · {panel.UbicacionesPendientes} solicitudes pendientes"));
        _contenido.Add(Texto($"Disponibilidad operativa · {panel.CantidadElegible} solicitudes · {panel.PesoElegible:0.##} kg elegibles"));
        foreach (var (texto, ruta) in new[] { ("Solicitudes", "solicitudes"), ("Residuos", "residuos"),
            ("Jornadas", "jornadas"), ("Estadísticas", "estadisticas") })
            _contenido.Add(Boton(texto, () => Shell.Current.GoToAsync("//" + ruta)));
    }

    private static Border Metrica(string etiqueta, string valor, string color)
    {
        var cifra = Texto(valor, "MetricValue");
        cifra.TextColor = Color.FromArgb(color);
        return Tarjeta(cifra, Texto(etiqueta));
    }

    private void MostrarJornadas()
    {
        if (_vm.Jornadas.Count == 0)
            _contenido.Add(Texto("No hay jornadas programadas disponibles."));
        foreach (var jornada in _vm.Jornadas)
        {
            _contenido.Add(Tarjeta(Texto(jornada.Zona, "SectionHeading"),
                Texto($"Fecha de recogida · {jornada.FechaProgramada:dd/MM/yyyy}"),
                Texto($"{jornada.CantidadSolicitudes} solicitudes · {jornada.PesoTotal:0.##} kg"),
                Boton("Consultar paradas", () => Navigation.PushModalAsync(DetalleCampoPage.ParaJornada(jornada.Id)), true)));
        }
    }

    internal static string Resumen(SolicitudRecoleccion solicitud, IReadOnlyList<ResiduoElectronico> equipos) =>
        $"{equipos.Count} registros · {equipos.Sum(e => (long)e.Cantidad)} unidades · {solicitud.PesoTotalEstimado:0.##} kg";
}
