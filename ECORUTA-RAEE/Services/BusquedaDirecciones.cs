using System.Net;
using System.Text.Json;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Services;

internal enum EstadoBusqueda { Resultados, SinResultados, SinClave, Limite, ClaveRechazada, NoDisponible }

internal sealed record DireccionEncontrada(string Texto, PuntoRecogida Punto, string Fuente);

internal sealed record BusquedaMapa(EstadoBusqueda Estado, IReadOnlyList<DireccionEncontrada> Direcciones)
{
    internal BusquedaMapa(EstadoBusqueda estado) : this(estado, Array.Empty<DireccionEncontrada>()) { }
}

internal sealed class BusquedaDirecciones(HttpClient http, TimeProvider reloj)
{
    private const int MaxResultados = 6;
    private readonly SemaphoreSlim _turno = new(1);
    private DateTimeOffset _proximaPeticion;
    private DateTimeOffset _pausaPorCuota;

    internal static BusquedaDirecciones Instancia { get; } = new(new HttpClient(
        new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = 256 * 1024
    }, TimeProvider.System);

    internal async Task<BusquedaMapa> BuscarAsync(string texto, string? clave, CancellationToken cancelar)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return new(EstadoBusqueda.SinClave);

        await _turno.WaitAsync(cancelar);
        try
        {
            var ahora = reloj.GetUtcNow();
            if (ahora < _pausaPorCuota)
                return new(EstadoBusqueda.Limite);

            if (ahora < _proximaPeticion)
                await Task.Delay(_proximaPeticion - ahora, reloj, cancelar);

            cancelar.ThrowIfCancellationRequested();
            _proximaPeticion = reloj.GetUtcNow().AddSeconds(1);
            // El sesgo usa un punto fijo en Santo Domingo Oeste. Solo se envía lo escrito en el buscador.
            string uri = "https://api.geoapify.com/v1/geocode/search?format=json&lang=es" +
                $"&limit={MaxResultados}&filter=countrycode:do&bias=proximity:-70.005,18.51" +
                $"&text={Uri.EscapeDataString(texto)}&apiKey={Uri.EscapeDataString(clave)}";
            using var respuesta = await http.GetAsync(uri, cancelar);
            if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
            {
                ahora = reloj.GetUtcNow();
                var espera = respuesta.Headers.RetryAfter;
                var hasta = espera?.Date ?? (espera?.Delta is { } demora ? ahora + demora : ahora.AddMinutes(1));
                _pausaPorCuota = hasta > ahora ? hasta : ahora.AddMinutes(1);
                return new(EstadoBusqueda.Limite);
            }

            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(EstadoBusqueda.ClaveRechazada);
            if (!respuesta.IsSuccessStatusCode)
                return new(EstadoBusqueda.NoDisponible);

            await using var contenido = await respuesta.Content.ReadAsStreamAsync(cancelar);
            using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: cancelar);
            if (!documento.RootElement.TryGetProperty("results", out var resultados) ||
                resultados.ValueKind != JsonValueKind.Array)
                return new(EstadoBusqueda.NoDisponible);

            var direcciones = new List<DireccionEncontrada>();
            foreach (var resultado in resultados.EnumerateArray())
            {
                if (resultado.ValueKind != JsonValueKind.Object ||
                    !string.Equals(LeerTexto(resultado, "country_code"), "do", StringComparison.OrdinalIgnoreCase) ||
                    !LeerNumero(resultado, "lat", out double latitud) ||
                    !LeerNumero(resultado, "lon", out double longitud) ||
                    PuntoRecogida.Crear(latitud, longitud) is not { } punto ||
                    LeerTexto(resultado, "formatted") is not { Length: > 0 } etiqueta)
                    continue;

                string fuente = resultado.TryGetProperty("datasource", out var origen) &&
                    origen.ValueKind == JsonValueKind.Object ? LeerTexto(origen, "attribution") ?? "" : "";
                if (!direcciones.Any(d => d.Punto == punto && d.Texto == etiqueta))
                    direcciones.Add(new(etiqueta, punto, fuente));
                if (direcciones.Count == MaxResultados)
                    break;
            }
            return direcciones.Count == 0 ? new(EstadoBusqueda.SinResultados)
                : new(EstadoBusqueda.Resultados, direcciones);
        }
        catch (Exception ex) when ((ex is HttpRequestException or IOException or JsonException or InvalidOperationException ||
            ex is OperationCanceledException && !cancelar.IsCancellationRequested))
        {
            // El mensaje de HttpClient puede contener la URL con la consulta y la clave.
            return new(EstadoBusqueda.NoDisponible);
        }
        finally
        {
            _turno.Release();
        }
    }

    private static string? LeerTexto(JsonElement valor, string propiedad) =>
        valor.TryGetProperty(propiedad, out var campo) && campo.ValueKind == JsonValueKind.String
            ? campo.GetString()?.Trim() : null;

    private static bool LeerNumero(JsonElement valor, string propiedad, out double numero)
    {
        numero = 0;
        return valor.TryGetProperty(propiedad, out var campo) &&
            campo.ValueKind == JsonValueKind.Number && campo.TryGetDouble(out numero);
    }
}
