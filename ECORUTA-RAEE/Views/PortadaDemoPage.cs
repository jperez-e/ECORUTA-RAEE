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
            var (titulo, detalle) = _perfil switch
            {
                PerfilDemostracion.Ciudadano => ("Pequeñas acciones, grandes cambios.", "Dale una segunda vida a tus equipos electrónicos."),
                PerfilDemostracion.Gestor => ("Centro de operaciones", "Organiza las recogidas y supervisa la demanda de tu comunidad."),
                _ => ("Cada recogida cuenta.", "Consulta las jornadas disponibles y organiza tus paradas.")
            };
            var nuevaSolicitud = _perfil == PerfilDemostracion.Ciudadano
                ? Boton("Nueva solicitud", () => Shell.Current.GoToAsync(nameof(SolicitudFormPage)), true)
                : null;
            var cabecera = Cabecera(titulo, detalle, nuevaSolicitud);
            bool primeraCarga = _contenido.Children.Count == 0;
            var estado = Texto("Cargando…");
            if (primeraCarga)
            {
                _contenido.Add(cabecera);
                _contenido.Add(estado);
            }
            if (!await _vm.CargarAsync())
            {
                estado.Text = "No se pudieron actualizar los datos.";
                if (!_contenido.Children.Contains(estado))
                    _contenido.Insert(0, estado);
                return;
            }
            if (_perfil == PerfilDemostracion.Recolector)
            {
                var resumenCampo = CrearCuadricula();
                resumenCampo.Add(new MetricaVisual { Etiqueta = "Jornadas disponibles", Icono = "calendario",
                    Valor = _vm.PortadaCampo.Count });
                resumenCampo.Add(new MetricaVisual { Etiqueta = "Paradas programadas", Icono = "punto",
                    Valor = _vm.PortadaCampo.Sum(j => j.Paradas) }, 1, 0);
                cabecera = Cabecera(titulo, detalle, resumenCampo);
            }
            if (_perfil == PerfilDemostracion.Gestor && _vm.Panel is { } panel)
            {
                var resumenGestor = CrearCuadricula();
                resumenGestor.Add(new MetricaVisual { Etiqueta = "Solicitudes registradas", Icono = "panel", Valor = panel.Indicadores.Solicitudes });
                resumenGestor.Add(new MetricaVisual { Etiqueta = "Peso registrado", Icono = "peso",
                    Valor = $"{panel.Indicadores.PesoVigente:0.##} kg" }, 1, 0);
                cabecera = Cabecera(titulo, detalle, resumenGestor);
            }
            _contenido.Clear();
            _contenido.Add(cabecera);
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
        var recientes = CrearCuadricula();
        recientes.Add(Texto("Solicitudes recientes", "TituloVisual"));
        recientes.Add(Boton("Ver todas", () => Navigation.PushModalAsync(new ConsultaCiudadanaPage())), 1, 0);
        _contenido.Add(recientes);
        if (_vm.VistaCiudadana.Count == 0)
            _contenido.Add(Tarjeta(ConIcono("equipo", Texto("Todavía no hay solicitudes registradas."))));
        foreach (var fila in _vm.VistaCiudadana.Take(3))
            _contenido.Add(ConsultaCiudadanaPage.TarjetaSolicitud(fila, Navigation));
        _contenido.Add(Boton("¿Qué equipos puedo registrar?", () => DisplayAlertAsync(
            "Categorías disponibles", string.Join(", ", DatosEquipo.Categorias) +
            ". Elige la categoría y describe los equipos que quieres entregar.", "Entendido")));
        _contenido.Add(Boton("¿Cómo funciona EcoRuta?", () => DisplayAlertAsync(
            "Cómo funciona EcoRuta", "Registra tus equipos y añade un punto en el mapa. Sin internet puedes guardar la referencia y completar el punto después. Consulta aquí la fecha si se programa una jornada.", "Entendido")));
    }

    private void MostrarPanel()
    {
        if (_vm.Panel is not { } panel)
            return;
        _contenido.Add(Texto("Resumen operativo", "TituloVisual"));
        var metricas = CrearCuadricula();
        metricas.Add(new MetricaVisual { Etiqueta = "Zonas listas", Icono = "listo", Valor = panel.Alertas.Count(a => a.EsLista) });
        metricas.Add(new MetricaVisual { Etiqueta = "Zonas próximas", Icono = "reloj", Tono = "ambar",
            Valor = panel.Alertas.Count(a => a.Nivel == NivelAlertaZona.Proxima) }, 1, 0);
        metricas.Add(new MetricaVisual { Etiqueta = "Jornadas programadas", Icono = "calendario", Tono = "azul", Valor = panel.Jornadas.Programadas }, 0, 1);
        metricas.Add(new MetricaVisual { Etiqueta = "Ubicaciones pendientes", Icono = "punto", Tono = "ambar", Valor = panel.UbicacionesPendientes }, 1, 1);
        _contenido.Add(metricas);
        _contenido.Add(Texto($"{panel.CantidadElegible} solicitudes · {panel.PesoElegible:0.##} kg elegibles para jornadas"));
        _contenido.Add(Texto("Requieren atención", "TituloVisual"));
        if (_vm.AlertasGestion.Count == 0)
            _contenido.Add(Tarjeta(Texto("No hay alertas operativas.")));
        foreach (var alerta in _vm.AlertasGestion)
            _contenido.Add(Tarjeta(ConIcono(alerta.Icono, Texto(alerta.Titulo, "TituloVisual")),
                Texto(alerta.Detalle), Boton(alerta.Accion, () => Shell.Current.GoToAsync(alerta.Ruta,
                    new ShellNavigationQueryParameters { [alerta.Clave] = alerta.Valor! }))));
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
    }

    private static Grid CrearCuadricula() => new()
    {
        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
        RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) }, ColumnSpacing = 8, RowSpacing = 8
    };

    private void MostrarJornadas()
    {
        _contenido.Add(Texto("Próximas jornadas", "TituloVisual"));
        if (_vm.PortadaCampo.Count == 0)
            _contenido.Add(Tarjeta(ConIcono("calendario", Texto("No hay jornadas programadas disponibles."))));
        foreach (var jornada in _vm.PortadaCampo)
        {
            var totales = new Grid
            {
                ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) },
                ColumnSpacing = 6
            };
            totales.Add(Texto($"{jornada.Paradas} paradas"));
            totales.Add(Texto(ParadaCampo.UnidadesVisibles(jornada.Unidades)), 1, 0);
            totales.Add(Texto($"{jornada.Peso:0.##} kg estimados"), 2, 0);
            _contenido.Add(Tarjeta(ConIcono("camion", Texto(jornada.Zona, "TituloVisual")),
                EstadoVisual("Programada", "azul"),
                ConIcono("calendario", Texto(jornada.Fecha.ToString("d 'de' MMMM 'de' yyyy"))),
                totales, EstadoVisual(jornada.EstadoOrden,
                    jornada.EstadoOrden == "Orden de paradas disponible" ? "verde" : "ambar"),
                Boton("Ver jornada", () => Navigation.PushModalAsync(DetalleCampoPage.ParaJornada(jornada.Id)), true)));
        }
    }

    internal static string Resumen(SolicitudRecoleccion solicitud, IReadOnlyList<ResiduoElectronico> equipos) =>
        $"{equipos.Count} registros · {equipos.Sum(e => (long)e.Cantidad)} unidades · {solicitud.PesoTotalEstimado:0.##} kg";
}
