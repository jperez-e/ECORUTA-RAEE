using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.Views;

public partial class SelectorUbicacionPage
{
    private const string MensajeInicialBusqueda = "Solo se envía lo que escribas aquí al pulsar Buscar.";
    private const string ArchivoClave = "Mapa/geoapify-key.txt";
    private CancellationTokenSource? _busqueda;

    private void Consulta_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _busqueda?.Cancel();
        _busqueda = null;
        OcultarResultados();
        EstadoBusquedaLabel.Text = MensajeInicialBusqueda;
    }

    private async void Buscar_Clicked(object? sender, EventArgs e)
    {
        if (_cerrando || _busqueda is not null)
            return;

        string texto = Consulta.Text?.Trim() ?? "";
        if (texto.Length < 3)
        {
            EstadoBusquedaLabel.Text = "Escribe al menos 3 caracteres para buscar.";
            return;
        }
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            EstadoBusquedaLabel.Text = "Sin conexión. Puedes volver y escribir la referencia.";
            return;
        }

        OcultarResultados();
        using var peticion = CancellationTokenSource.CreateLinkedTokenSource(_vida.Token);
        _busqueda = peticion;
        EstadoBusquedaLabel.Text = "Buscando…";
        try
        {
            await Consulta.HideSoftInputAsync(peticion.Token);
            Consulta.Unfocus();
            await Task.Delay(350, peticion.Token);
            var respuesta = await BusquedaDirecciones.Instancia.BuscarAsync(texto,
                await LeerClaveAsync(peticion.Token), peticion.Token);
            if (_busqueda != peticion || peticion.IsCancellationRequested)
                return;

            EstadoBusquedaLabel.Text = respuesta.Estado switch
            {
                EstadoBusqueda.Resultados => "Elige un resultado y comprueba el punto en el mapa.",
                EstadoBusqueda.SinResultados => "Sin resultados. Prueba con otra calle o sector.",
                EstadoBusqueda.SinClave => "Búsqueda sin configurar. Puedes usar el mapa o el GPS.",
                EstadoBusqueda.Limite => "Límite de búsquedas alcanzado. Intenta más tarde o usa el mapa.",
                EstadoBusqueda.ClaveRechazada => "Búsqueda no autorizada. Puedes usar el mapa o el GPS.",
                _ => "No se pudo buscar. Revisa la conexión o usa el mapa."
            };
            if (respuesta.Estado == EstadoBusqueda.Resultados)
            {
                BindableLayout.SetItemsSource(ListaResultados, respuesta.Direcciones);
                PanelResultados.IsVisible = true;
            }
        }
        catch (OperationCanceledException) when (peticion.IsCancellationRequested)
        {
        }
        finally
        {
            if (_busqueda == peticion)
                _busqueda = null;
        }
    }

    private static async Task<string?> LeerClaveAsync(CancellationToken cancelar)
    {
        try
        {
            if (!await FileSystem.AppPackageFileExistsAsync(ArchivoClave))
                return null;

            using var archivo = await FileSystem.OpenAppPackageFileAsync(ArchivoClave);
            using var lector = new StreamReader(archivo);
            return (await lector.ReadToEndAsync(cancelar)).Trim();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private async void Resultado_Clicked(object? sender, EventArgs e)
    {
        if (_cerrando || _ocupada || sender is not Button { CommandParameter: DireccionEncontrada resultado })
            return;
        if (!_lista)
        {
            Estado.Text = "Espera a que se abra el mapa para elegir el resultado.";
            return;
        }

        _ocupada = true;
        ActualizarBotones();
        try
        {
            if (await ColocarPuntoAsync(resultado.Punto))
            {
                OcultarResultados();
                EstadoBusquedaLabel.Text = "Ubicación aproximada. Ajusta el marcador si hace falta.";
                Estado.Text = "Confirma el punto para devolverlo al formulario.";
            }
            else
                Estado.Text = "No se pudo colocar el marcador. Puedes elegir un punto en el mapa.";
        }
        catch (OperationCanceledException) when (_cerrando)
        {
        }
        catch (Exception)
        {
            Estado.Text = "No se pudo colocar el marcador. Puedes elegir un punto en el mapa.";
        }
        finally
        {
            _ocupada = false;
            ActualizarBotones();
        }
    }

    private void OcultarResultados_Clicked(object? sender, EventArgs e)
    {
        OcultarResultados();
        EstadoBusquedaLabel.Text = MensajeInicialBusqueda;
    }

    private void OcultarResultados()
    {
        PanelResultados.IsVisible = false;
        BindableLayout.SetItemsSource(ListaResultados, null);
    }

    private async void Geoapify_Tapped(object? sender, TappedEventArgs e) =>
        await AbrirEnNavegadorAsync("https://www.geoapify.com/");
}
