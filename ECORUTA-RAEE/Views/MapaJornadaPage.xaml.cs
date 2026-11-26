using System.Text.Json;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class MapaJornadaPage : ContentPage
{
    private const string Atribucion = "https://www.openstreetmap.org/copyright";
    private const string Origen = "about:blank";
    private readonly int _jornadaId;
    private readonly MapaJornada? _ubicacion;
    private readonly bool _consultaDeCampo;
    private readonly string _sesion = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _vida = new();
    private bool _iniciada;
    private bool _lista;

    internal MapaJornadaPage(int jornadaId, bool consultaDeCampo = false)
    {
        InitializeComponent();
        _jornadaId = jornadaId;
        _consultaDeCampo = consultaDeCampo;
    }

    internal MapaJornadaPage(MapaJornada ubicacion)
    {
        InitializeComponent();
        Title = "Ubicación de recogida";
        _ubicacion = ubicacion;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_iniciada)
            return;
        _iniciada = true;

        BindingContext = _ubicacion;
        bool consultada = _ubicacion is not null || await OperacionBaseDatos.EjecutarAsync(async () =>
        {
            var jornada = await JornadaService.Instancia.ObtenerDetalleAsync(_jornadaId, incluirRutaCancelada: true);
            if (jornada is null || _vida.IsCancellationRequested)
                return;
            var resultado = MapaJornada.Crear(jornada);
            if (_consultaDeCampo && jornada.Estado == JornadaRecoleccion.Programada && jornada.Ruta is null)
                resultado = resultado with { Metodo = jornada.RutaDesactualizada
                    ? "Orden desactualizado. Informa al gestor."
                    : "El gestor aún no ha guardado un orden." };
            BindingContext = resultado;
        });
        if (_vida.IsCancellationRequested)
            return;
        if (BindingContext is not MapaJornada datos)
        {
            EstadoMapa.Text = consultada ? "La jornada ya no existe." : "No se pudo consultar la jornada.";
            return;
        }

        Aviso.IsVisible = datos.SinCoordenadas > 0;
        Aviso.Text = $"Ubicaciones incompletas · {datos.SinCoordenadas}. Consulta el listado y completa los puntos antes de la recogida en campo.";
        if (!datos.Paradas.Any(p => p.Ubicada))
        {
            EstadoMapa.Text = "No hay puntos para ubicar en el mapa.";
            return;
        }

        try
        {
            bool internet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            EstadoMapa.Text = internet ? "Cargando mapa…" : "Sin conexión. Puedes consultar todas las paradas.";
            var recursos = await Task.WhenAll(LeerAsync("ruta.html"), LeerAsync("Leaflet/leaflet.css"),
                LeerAsync("Leaflet/leaflet.js"), LeerAsync("ruta.js"));
            _vida.Token.ThrowIfCancellationRequested();
            string config = JsonSerializer.Serialize(new { sesion = _sesion, internet, paradas = datos.Paradas },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Mapa.Source = new HtmlWebViewSource { BaseUrl = Origen, Html = recursos[0]
                .Replace("/*LEAFLET_CSS*/", recursos[1]).Replace("/*LEAFLET_JS*/", recursos[2])
                .Replace("/*RUTA_JS*/", recursos[3]).Replace("/*CONFIG*/", config) };

            using var espera = CancellationTokenSource.CreateLinkedTokenSource(_vida.Token);
            espera.CancelAfter(TimeSpan.FromSeconds(15));
            while (!_lista)
            {
                await Task.Delay(250, espera.Token);
                string sesion = await Mapa.EvaluateJavaScriptAsync("window.ecorutaRuta?.sesion").WaitAsync(espera.Token);
                _lista = sesion == _sesion || sesion == JsonSerializer.Serialize(_sesion);
            }
            Todas.IsEnabled = true;
            if (internet)
                EstadoMapa.Text = "Toca un marcador o elige una parada del listado.";
        }
        catch (OperationCanceledException) when (_vida.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
        {
            EstadoMapa.Text = "No se pudo mostrar el mapa. El listado sigue disponible.";
        }
    }

    private static async Task<string> LeerAsync(string nombre)
    {
        using var archivo = await FileSystem.OpenAppPackageFileAsync("Mapa/" + nombre);
        using var lector = new StreamReader(archivo);
        return await lector.ReadToEndAsync();
    }

    private async void Mapa_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!_lista && (string.IsNullOrEmpty(e.Url) || e.Url == Origen))
            return;
        e.Cancel = true;
        if (e.Url == Atribucion)
        {
            try { await Launcher.Default.OpenAsync(new Uri(Atribucion)); }
            catch (Exception) { EstadoMapa.Text = "No se pudo abrir la atribución."; }
        }
    }

    private async void Todas_Clicked(object? sender, EventArgs e) => await EjecutarMapaAsync("mostrarTodas()");

    private async void Parada_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: ParadaMapa { Ubicada: true } parada })
            await EjecutarMapaAsync(FormattableString.Invariant($"centrar({parada.Id})"));
    }

    private async Task EjecutarMapaAsync(string accion)
    {
        if (!_lista || _vida.IsCancellationRequested)
            return;
        try
        {
            await Mapa.EvaluateJavaScriptAsync("window.ecorutaRuta?." + accion)
                .WaitAsync(TimeSpan.FromSeconds(5), _vida.Token);
        }
        catch (OperationCanceledException) when (_vida.IsCancellationRequested) { }
        catch (Exception) { EstadoMapa.Text = "No se pudo centrar el mapa. Consulta el listado."; }
    }

    private async void Volver_Clicked(object? sender, EventArgs e)
    {
        if (_vida.IsCancellationRequested)
            return;
        _vida.Cancel();
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _vida.Cancel();
        return base.OnBackButtonPressed();
    }
}
