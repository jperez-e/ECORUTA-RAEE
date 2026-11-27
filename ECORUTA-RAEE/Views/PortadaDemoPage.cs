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
        Style = Estilo("CanvasVisual");
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
            var (titulo, detalle) = _perfil switch
            {
                PerfilDemostracion.Ciudadano => ("Dale una segunda vida a tus equipos", "Solicita la recogida de tus residuos electrónicos."),
                PerfilDemostracion.Gestor => ("Centro de gestión", "Planifica y coordina las recogidas de residuos electrónicos."),
                _ => ("Panel de recolección", "Consulta las jornadas y los puntos de recogida.")
            };
            var nuevaSolicitud = _perfil == PerfilDemostracion.Ciudadano
                ? Boton("Nueva solicitud", () => Shell.Current.GoToAsync(nameof(SolicitudFormPage)), true)
                : null;
            var cabecera = Cabecera(titulo, detalle, nuevaSolicitud);
            _contenido.Add(cabecera);
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
        var resumen = CrearCuadricula();
        resumen.Add(new MetricaVisual { Etiqueta = "Solicitudes registradas", Icono = "panel", Valor = _vm.Solicitudes.Count }, 0, 0);
        resumen.Add(new MetricaVisual { Etiqueta = "Peso registrado", Icono = "peso", Tono = "verde",
            Valor = $"{_vm.Solicitudes.Sum(s => s.PesoTotalEstimado):0.##} kg" }, 1, 0);
        _contenido.Add(resumen);
        _contenido.Add(Texto("Solicitudes de este dispositivo", "TituloVisual"));
        if (_vm.Solicitudes.Count == 0)
            _contenido.Add(Tarjeta(ConIcono("equipo", Texto("Todavía no hay solicitudes registradas."))));
        foreach (var solicitud in _vm.Solicitudes)
        {
            var equipos = _vm.Equipos(solicitud.Id).ToArray();
            var cabecera = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 6 };
            cabecera.Add(Texto(solicitud.CodigoSolicitud, "TituloVisual"), 0, 0);
            cabecera.Add(EstadoVisual(solicitud.Estado, solicitud.Estado == ElegibilidadRecogida.Pendiente ? "ambar" : "azul"), 1, 0);
            var tarjeta = new List<View> { cabecera,
                Texto(solicitud.Zona), Texto(solicitud.DireccionReferencia),
                Texto(Resumen(solicitud, equipos)),
                Boton("Consultar solicitud", () => Navigation.PushModalAsync(
                    DetalleCampoPage.ParaSolicitud(solicitud, equipos))) };
            if (solicitud.UbicacionPendiente)
            {
                tarjeta.Add(ConIcono("punto", EstadoVisual("Ubicación por completar. La solicitud todavía no es elegible para una jornada automática.", "ambar")));
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
        var metricas = CrearCuadricula();
        metricas.Add(new MetricaVisual { Etiqueta = "Solicitudes pendientes", Icono = "reloj", Tono = "ambar", Valor = panel.Indicadores.Pendientes }, 0, 0);
        metricas.Add(new MetricaVisual { Etiqueta = "Zonas listas", Icono = "listo", Valor = panel.Alertas.Count(a => a.EsLista) }, 1, 0);
        metricas.Add(new MetricaVisual { Etiqueta = "Jornadas programadas", Icono = "calendario", Tono = "azul", Valor = panel.Jornadas.Programadas }, 0, 1);
        metricas.Add(new MetricaVisual { Etiqueta = "Peso vigente registrado", Icono = "peso", Tono = "violeta", Valor = $"{panel.Indicadores.PesoVigente:0.##} kg" }, 1, 1);
        _contenido.Add(metricas);
        _contenido.Add(Texto("Accesos rápidos", "TituloVisual"));
        var acciones = CrearCuadricula();
        var enlaces = new[] { ("Solicitudes", "solicitudes", "panel"), ("Residuos", "residuos", "equipo"),
            ("Jornadas", "jornadas", "calendario"), ("Estadísticas", "estadisticas", "peso") };
        for (int i = 0; i < enlaces.Length; i++)
        {
            var (texto, ruta, icono) = enlaces[i];
            var boton = Boton(texto, () => Shell.Current.GoToAsync("//" + ruta));
            boton.ImageSource = $"ui_{icono}.png";
            boton.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 3);
            acciones.Add(boton, i % 2, i / 2);
        }
        _contenido.Add(acciones);
        var proximas = panel.Alertas.Where(a => a.Nivel == NivelAlertaZona.Proxima).ToArray();
        _contenido.Add(Tarjeta(
            ConIcono("punto", EstadoVisual($"Ubicación por completar · {panel.UbicacionesPendientes} solicitudes pendientes", "ambar")),
            Texto($"Disponibilidad operativa · {panel.CantidadElegible} solicitudes · {panel.PesoElegible:0.##} kg elegibles")));
        var alertas = new VerticalStackLayout { Spacing = 6 };
        alertas.Add(EstadoVisual($"Alertas operativas · {proximas.Length} próximas", "ambar"));
        if (panel.Alertas.Count == 0)
            alertas.Add(Texto("No hay zonas listas ni próximas al umbral."));
        foreach (var zona in panel.Alertas)
            alertas.Add(Texto($"{zona.Zona} · {zona.EstadoAlerta}"));
        _contenido.Add(new Border { Style = Estilo("PanelVisual"), Content = alertas });

    }

    private static Grid CrearCuadricula() => new()
    {
        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
        RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) }, ColumnSpacing = 8, RowSpacing = 8
    };

    private void MostrarJornadas()
    {
        _contenido.Add(Texto("Jornadas programadas disponibles", "TituloVisual"));
        if (_vm.Jornadas.Count == 0)
            _contenido.Add(Tarjeta(ConIcono("calendario", Texto("No hay jornadas programadas disponibles."))));
        foreach (var jornada in _vm.Jornadas)
        {
            _contenido.Add(Tarjeta(ConIcono("camion", Texto($"Recogida en {jornada.Zona}", "TituloVisual")),
                EstadoVisual("Jornada programada", "azul"),
                Texto($"Fecha de recogida · {jornada.FechaProgramada:dd/MM/yyyy}"),
                Texto($"{jornada.CantidadSolicitudes} solicitudes · {jornada.PesoTotal:0.##} kg"),
                Boton("Ver ruta en el mapa", () => Navigation.PushModalAsync(new MapaJornadaPage(jornada.Id, consultaDeCampo: true)), true),
                Boton("Consultar paradas", () => Navigation.PushModalAsync(DetalleCampoPage.ParaJornada(jornada.Id)))));
        }
    }

    internal static string Resumen(SolicitudRecoleccion solicitud, IReadOnlyList<ResiduoElectronico> equipos) =>
        $"{equipos.Count} registros · {equipos.Sum(e => (long)e.Cantidad)} unidades · {solicitud.PesoTotalEstimado:0.##} kg";
}
