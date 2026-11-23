using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Views;

public partial class SelectorUbicacionPage : ContentPage
{
    private const string OrigenLocal = "about:blank";
    private const string Atribucion = "https://www.openstreetmap.org/copyright";
    private readonly string _sesion = Guid.NewGuid().ToString("N");
    private readonly PuntoRecogida? _inicial;
    private readonly Action<PuntoRecogida> _alConfirmar;
    private readonly CancellationTokenSource _vida = new();
    private bool _iniciada;
    private bool _lista;
    private bool _ocupada;
    private bool _cerrando;

    internal SelectorUbicacionPage(PuntoRecogida? inicial, Action<PuntoRecogida> alConfirmar)
    {
        InitializeComponent();
        _inicial = inicial;
        _alConfirmar = alConfirmar;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_iniciada)
            return;

        _iniciada = true;
        try
        {
            var recursos = await Task.WhenAll(
                LeerRecursoAsync("selector.html"), LeerRecursoAsync("Leaflet/leaflet.css"),
                LeerRecursoAsync("Leaflet/leaflet.js"), LeerRecursoAsync("selector.js"));
            if (_cerrando)
                return;

            bool internet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            string config = JsonSerializer.Serialize(new
            {
                sesion = _sesion,
                inicial = _inicial is null ? null : new[] { _inicial.Latitud, _inicial.Longitud },
                internet
            });
            string html = recursos[0]
                .Replace("<!--LEAFLET_CSS-->", recursos[1])
                .Replace("/*LEAFLET_JS*/", recursos[2])
                .Replace("/*SELECTOR_JS*/", recursos[3])
                .Replace("/*CONFIG*/", config);
            Mapa.Source = new HtmlWebViewSource { Html = html, BaseUrl = OrigenLocal };
            await EsperarMapaAsync(internet);
        }
        catch (OperationCanceledException) when (_cerrando)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Debug.WriteLine(ex);
            Estado.Text = "No se pudo abrir el mapa. Vuelve al formulario para continuar.";
        }
    }

    private static async Task<string> LeerRecursoAsync(string nombre)
    {
        using var recurso = await FileSystem.OpenAppPackageFileAsync("Mapa/" + nombre);
        using var lector = new StreamReader(recurso);
        return await lector.ReadToEndAsync();
    }

    private async void Mapa_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!_lista && (string.IsNullOrEmpty(e.Url) || e.Url == OrigenLocal))
            return;

        e.Cancel = true;
        if (e.Url == Atribucion)
            await AbrirEnNavegadorAsync(Atribucion);
    }

    private async Task EsperarMapaAsync(bool internet)
    {
        // Android may omit Navigated for an inline HtmlWebViewSource.
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(_vida.Token);
        espera.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            while (!_lista)
            {
                await Task.Delay(250, espera.Token);
                var sesion = await Mapa.EvaluateJavaScriptAsync("window.ecoruta?.sesion")
                    .WaitAsync(espera.Token);
                _lista = sesion == _sesion || sesion == JsonSerializer.Serialize(_sesion);
            }
        }
        catch (OperationCanceledException) when (!_cerrando)
        {
            Estado.Text = "No se pudo cargar el selector. Puedes volver al formulario.";
            return;
        }
        ActualizarBotones();
        Estado.Text = internet ? "Toca el mapa o usa tu ubicación. Confirma el punto para volver."
            : "Sin internet. Puedes volver y guardar la referencia o introducir coordenadas avanzadas.";
    }

    private void ActualizarBotones()
    {
        Gps.IsEnabled = Confirmar.IsEnabled = _lista && !_ocupada && !_cerrando;
    }

    private async void Gps_Clicked(object? sender, EventArgs e)
    {
        if (!_lista || _ocupada || _cerrando)
            return;

        _ocupada = true;
        ActualizarBotones();
        try
        {
            Estado.Text = "Buscando tu ubicación…";
            var permiso = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (_cerrando)
                return;
            if (permiso != PermissionStatus.Granted)
            {
                Estado.Text = "Permiso de ubicación denegado. Puedes elegir un punto en el mapa.";
                return;
            }

            var solicitud = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12));
            var ubicacion = await Geolocation.Default.GetLocationAsync(solicitud, _vida.Token);
            if (_cerrando)
                return;

            var punto = ubicacion is null ? null : PuntoRecogida.Crear(ubicacion.Latitude, ubicacion.Longitude);
            if (punto is null)
            {
                Estado.Text = "No se obtuvo una posición. Inténtalo de nuevo o elige un punto en el mapa.";
                return;
            }

            OcultarResultados();
            Estado.Text = await ColocarPuntoAsync(punto)
                ? "Ubicación aproximada. Ajusta el marcador si hace falta y confirma el punto."
                : "No se pudo colocar el marcador. Puedes elegir un punto en el mapa.";
        }
        catch (FeatureNotEnabledException)
        {
            Estado.Text = "La ubicación del teléfono está desactivada. Actívala o usa el mapa.";
        }
        catch (PermissionException)
        {
            Estado.Text = "El teléfono no concedió permiso de ubicación. Puedes usar el mapa.";
        }
        catch (OperationCanceledException) when (_cerrando)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            Estado.Text = "No se pudo obtener la ubicación. Puedes elegir un punto en el mapa.";
        }
        finally
        {
            _ocupada = false;
            ActualizarBotones();
        }
    }

    private async void Confirmar_Clicked(object? sender, EventArgs e)
    {
        if (!_lista || _ocupada || _cerrando)
            return;

        _ocupada = true;
        ActualizarBotones();
        try
        {
            string? mensaje = await Mapa.EvaluateJavaScriptAsync("window.ecoruta?.leer()")
                .WaitAsync(TimeSpan.FromSeconds(5), _vida.Token);
            var punto = PuntoRecogida.LeerSeleccion(mensaje, _sesion);
            if (punto is null)
                Estado.Text = "Elige primero un punto válido en el mapa.";
            else if (!_cerrando)
                await CerrarAsync(punto);
        }
        catch (OperationCanceledException) when (_cerrando)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            Estado.Text = "No se pudo confirmar la ubicación. Inténtalo de nuevo o vuelve al formulario.";
        }
        finally
        {
            _ocupada = false;
            ActualizarBotones();
        }
    }

    private async Task<bool> ColocarPuntoAsync(PuntoRecogida punto)
    {
        string script = string.Create(CultureInfo.InvariantCulture,
            $"window.ecoruta?.colocar({punto.Latitud:R}, {punto.Longitud:R}, true)");
        var colocada = await Mapa.EvaluateJavaScriptAsync(script).WaitAsync(TimeSpan.FromSeconds(5), _vida.Token);
        return colocada == "true";
    }

    private async Task AbrirEnNavegadorAsync(string url)
    {
        try { await Launcher.Default.OpenAsync(new Uri(url)); }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            Estado.Text = "No se pudo abrir el navegador. Puedes seguir usando el selector.";
        }
    }

    private async void Volver_Clicked(object? sender, EventArgs e)
    {
        try { await CerrarAsync(null); }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine(ex);
            Estado.Text = "No se pudo cerrar el selector. Inténtalo de nuevo.";
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_cerrando)
            return true;

        _cerrando = true;
        _vida.Cancel();
        return base.OnBackButtonPressed();
    }

    private async Task CerrarAsync(PuntoRecogida? punto)
    {
        if (_cerrando || Navigation.ModalStack.LastOrDefault() != this)
            return;

        _cerrando = true;
        try { await Navigation.PopModalAsync(); }
        catch
        {
            _cerrando = false;
            throw;
        }
        _vida.Cancel();
        if (punto is not null)
            _alConfirmar(punto);
    }
}
