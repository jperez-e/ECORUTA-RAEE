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
    private readonly Action<SeleccionUbicacion> _alConfirmar;
    private readonly CancellationTokenSource _vida = new();
    private bool _iniciada;
    private bool _lista;
    private bool _ocupada;
    private bool _cerrando;

    private PuntoRecogida? _puntoPreparado;
    private PuntoRecogida? _puntoGps;
    private PuntoRecogida? _ultimoConsultado;
    private bool _consultaRealizada;
    private int _revision;
    private bool _actualizandoZona;
    private readonly bool _requierePunto;

    internal SelectorUbicacionPage(PuntoRecogida? inicial, string? zona, string? sector,
        Action<SeleccionUbicacion> alConfirmar, bool bloquearZona = false, bool requierePunto = false)
    {
        InitializeComponent();
        _inicial = inicial;
        _puntoPreparado = inicial;
        _alConfirmar = alConfirmar;
        _requierePunto = requierePunto;
        Zona.ItemsSource = bloquearZona && zona is not null ? new[] { zona } : ElegibilidadRecogida.Zonas.ToArray();
        Zona.SelectedItem = zona;
        Sector.Text = sector;
        Sector.IsEnabled = Zona.IsEnabled = !bloquearZona;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_iniciada)
            return;

        _iniciada = true;
        bool internet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        if (!internet)
        {
            SinMapa.IsVisible = true;
            MostrarZona("No hay conexión", "Elige la zona manualmente. Puedes conservar un punto previo o usar el GPS.");
            Estado.Text = "Puedes completar el punto después.";
            return;
        }
        try
        {
            var recursos = await Task.WhenAll(
                LeerRecursoAsync("selector.html"), LeerRecursoAsync("Leaflet/leaflet.css"),
                LeerRecursoAsync("Leaflet/leaflet.js"), LeerRecursoAsync("selector.js"));
            if (_cerrando)
                return;

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
            await EsperarMapaAsync();
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

    private async Task EsperarMapaAsync()
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
        Estado.Text = "Toca el mapa o usa tu ubicación.";
    }

    private void ActualizarBotones()
    {
        Gps.IsEnabled = !_ocupada && !_cerrando;
        ConsultarZona.IsEnabled = (_lista || _puntoGps is not null || _inicial is not null) &&
            !_ocupada && !_cerrando && Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        Confirmar.IsEnabled = !_ocupada && !_cerrando && (!_requierePunto || _puntoPreparado is not null) &&
            SeleccionUbicacion.Crear(_puntoPreparado, Zona.SelectedItem as string, Sector.Text) is not null;
        Confirmar.Text = _puntoPreparado is null ? "Confirmar zona" : "Confirmar ubicación";
    }

    private async void Gps_Clicked(object? sender, EventArgs e)
    {
        if (_ocupada || _cerrando)
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
            _puntoGps = punto;
            if (SinMapa.IsVisible || !_lista)
            {
                PrepararPunto(punto);
                MostrarZona("Ubicación del GPS", "Confirma la zona antes de continuar.");
                return;
            }
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

    private async Task<PuntoRecogida?> LeerPuntoAsync()
    {
        if (!_lista || SinMapa.IsVisible)
            return _puntoGps ?? _inicial;

        string? mensaje = await Mapa.EvaluateJavaScriptAsync("window.ecoruta?.leer()")
            .WaitAsync(TimeSpan.FromSeconds(5), _vida.Token);
        return PuntoRecogida.LeerSeleccion(mensaje, _sesion) ?? _puntoGps ?? _inicial;
    }

    private void MostrarZona(string titulo, string detalle)
    {
        PanelZona.IsVisible = true;
        TituloZona.Text = titulo;
        DetalleZona.Text = detalle;
        ActualizarBotones();
    }

    private void PrepararPunto(PuntoRecogida? punto)
    {
        if (punto == _puntoPreparado)
            return;
        _revision++;
        _puntoPreparado = punto;
        if (Zona.IsEnabled)
            Zona.SelectedItem = null;
    }

    private void Zona_Cambiada(object? sender, EventArgs e)
    {
        if (!_actualizandoZona)
            _revision++;
        Sector.IsVisible = Zona.SelectedItem as string == "Otra zona";
        ActualizarBotones();
    }

    private void Sector_Cambiado(object? sender, TextChangedEventArgs e) => ActualizarBotones();

    private async void ConsultarZona_Clicked(object? sender, EventArgs e) => await PrepararZonaAsync(true);
    private async void ModoManual_Clicked(object? sender, EventArgs e) => await PrepararZonaAsync(false);

    private async Task PrepararZonaAsync(bool consultar)
    {
        if (_cerrando || _ocupada)
            return;
        _ocupada = true;
        ActualizarBotones();
        try
        {
            var punto = await LeerPuntoAsync();
            if (consultar && punto is null)
            {
                Estado.Text = "Elige primero un punto válido en el mapa.";
                return;
            }
            PrepararPunto(punto);

            if (!Zona.IsEnabled)
            {
                MostrarZona("Zona de la jornada", "Para cambiarla, el gestor debe cancelar primero la jornada activa.");
                return;
            }
            if (!consultar || Connectivity.Current.NetworkAccess != NetworkAccess.Internet || punto == _inicial || (_consultaRealizada && punto == _ultimoConsultado))
            {
                MostrarZona("Confirma tu zona", "Selecciona la zona que corresponde al punto o a la referencia.");
                return;
            }

            MostrarZona("Buscando zona…", "También puedes seleccionar tu zona manualmente.");
            int revision = _revision;
            BuscandoZona.IsVisible = BuscandoZona.IsRunning = true;
            var resultado = await Services.BusquedaDirecciones.Instancia.SugerirZonaAsync(
                punto!, await LeerClaveAsync(_vida.Token), _vida.Token);
            var vigente = await LeerPuntoAsync();
            if (_cerrando)
                return;
            if (vigente != punto)
            {
                PrepararPunto(vigente);
                MostrarZona("El punto cambió", "Usa el punto actualizado para sugerir una zona o elígela manualmente.");
                return;
            }
            _ultimoConsultado = punto;
            _consultaRealizada = true;

            if (revision != _revision)
            {
                MostrarZona("Confirma tu zona", "Se conservó tu selección manual.");
                return;
            }

            if (resultado.Zona is { } zona)
            {
                _actualizandoZona = true;
                try { Zona.SelectedItem = zona; }
                finally { _actualizandoZona = false; }
                MostrarZona($"Zona sugerida: {zona}", "Confirma que corresponde al punto. Puedes corregir la sugerencia.");
                return;
            }

            MostrarZona("Necesitamos confirmar tu zona", resultado.Estado switch
            {
                Services.EstadoBusqueda.Limite => "La búsqueda alcanzó su límite. Elige la zona manualmente.",
                Services.EstadoBusqueda.SinResultados => "El mapa no identificó una zona clara del catálogo. Elígela manualmente.",
                _ => "La sugerencia no está disponible. Elige la zona manualmente."
            });
        }
        catch (OperationCanceledException) when (_cerrando) { }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            MostrarZona("Necesitamos confirmar tu zona", "Elige la zona manualmente o vuelve al formulario.");
        }
        finally
        {
            BuscandoZona.IsVisible = BuscandoZona.IsRunning = false;
            _ocupada = false;
            ActualizarBotones();
        }
    }

    private async void Confirmar_Clicked(object? sender, EventArgs e)
    {
        if (_ocupada || _cerrando)
            return;
        _ocupada = true;
        ActualizarBotones();
        try
        {
            var punto = await LeerPuntoAsync();
            if (punto != _puntoPreparado)
            {
                PrepararPunto(punto);
                MostrarZona("El punto cambió", "Confirma la zona del punto actualizado.");
                return;
            }
            if ((!_requierePunto || punto is not null) &&
                SeleccionUbicacion.Crear(punto, Zona.SelectedItem as string, Sector.Text) is { } seleccion)
                await CerrarAsync(seleccion);
        }
        catch (OperationCanceledException) when (_cerrando) { }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            Estado.Text = "No se pudo confirmar. Inténtalo de nuevo o vuelve al formulario.";
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

    private async Task CerrarAsync(SeleccionUbicacion? seleccion)
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
        if (seleccion is not null)
            _alConfirmar(seleccion);
    }
}
