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

    internal Task<BusquedaMapa> BuscarAsync(string texto, string? clave, CancellationToken cancelar) =>
        ConsultarAsync($"search?format=json&lang=es&limit={MaxResultados}&filter=countrycode:do" +
            $"&bias=proximity:-70.005,18.51&text={Uri.EscapeDataString(texto)}",
            clave, LeerDirecciones, estado => new BusquedaMapa(estado), cancelar);

    internal Task<(EstadoBusqueda Estado, string? Zona)> SugerirZonaAsync(
        PuntoRecogida punto, string? clave, CancellationToken cancelar) =>
        ConsultarAsync(FormattableString.Invariant($"reverse?format=json&lang=es&limit=3&lat={punto.Latitud:R}&lon={punto.Longitud:R}"),
            clave, resultados =>
            {
                var zona = IdentificarZona(resultados);
                return (zona is null ? EstadoBusqueda.SinResultados : EstadoBusqueda.Resultados, zona);
            }, estado => (estado, (string?)null), cancelar);

    private async Task<T> ConsultarAsync<T>(string consulta, string? clave, Func<JsonElement, T> leer,
        Func<EstadoBusqueda, T> error, CancellationToken cancelar)
    {
        cancelar.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(clave))
            return error(EstadoBusqueda.SinClave);

        await _turno.WaitAsync(cancelar);
        try
        {
            var ahora = reloj.GetUtcNow();
            if (ahora < _pausaPorCuota)
                return error(EstadoBusqueda.Limite);

            if (ahora < _proximaPeticion)
                await Task.Delay(_proximaPeticion - ahora, reloj, cancelar);

            cancelar.ThrowIfCancellationRequested();
            _proximaPeticion = reloj.GetUtcNow().AddSeconds(1);
            string uri = $"https://api.geoapify.com/v1/geocode/{consulta}&apiKey={Uri.EscapeDataString(clave)}";
            using var respuesta = await http.GetAsync(uri, cancelar);
            if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
            {
                ahora = reloj.GetUtcNow();
                var espera = respuesta.Headers.RetryAfter;
                var hasta = espera?.Date ?? (espera?.Delta is { } demora ? ahora + demora : ahora.AddMinutes(1));
                _pausaPorCuota = hasta > ahora ? hasta : ahora.AddMinutes(1);
                return error(EstadoBusqueda.Limite);
            }

            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return error(EstadoBusqueda.ClaveRechazada);
            if (!respuesta.IsSuccessStatusCode)
                return error(EstadoBusqueda.NoDisponible);

            await using var contenido = await respuesta.Content.ReadAsStreamAsync(cancelar);
            using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: cancelar);
            if (!documento.RootElement.TryGetProperty("results", out var resultados) ||
                resultados.ValueKind != JsonValueKind.Array)
                return error(EstadoBusqueda.NoDisponible);

            cancelar.ThrowIfCancellationRequested();
            return leer(resultados);
        }
        catch (Exception ex) when ((ex is HttpRequestException or IOException or JsonException or InvalidOperationException ||
            ex is OperationCanceledException && !cancelar.IsCancellationRequested))
        {
            // El mensaje de HttpClient puede contener la URL con la consulta y la clave.
            return error(EstadoBusqueda.NoDisponible);
        }
        finally
        {
            _turno.Release();
        }
    }

    private static BusquedaMapa LeerDirecciones(JsonElement resultados)
    {
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

    internal static string? IdentificarZona(JsonElement resultados)
    {
        var coincidencias = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lugar in resultados.EnumerateArray())
        {
            if (lugar.ValueKind != JsonValueKind.Object ||
                !string.Equals(LeerTexto(lugar, "country_code"), "do", StringComparison.OrdinalIgnoreCase))
                continue;

            var tipo = LeerTexto(lugar, "result_type");
            if (tipo is not ("building" or "amenity" or "street" or "suburb" or "district"))
                continue;

            // Los componentes locales tienen precedencia sobre el municipio. Así Pantoja
            // puede sugerirse aunque la ciudad administrativa sea Los Alcarrizos.
            var locales = new[] { LeerTexto(lugar, "suburb"), LeerTexto(lugar, "district"),
                LeerTexto(lugar, "neighbourhood"), LeerTexto(lugar, "quarter"),
                tipo is "suburb" or "district" ? LeerTexto(lugar, "name") : null };
            var zonas = locales.Select(CoincidirZona).OfType<string>().Distinct().ToArray();
            if (zonas.Length > 1)
                return null;
            if (zonas.Length == 1)
                coincidencias.Add(zonas[0]);
        }
        return coincidencias.Count == 1 ? coincidencias.Single() : null;
    }

    private static string? CoincidirZona(string? texto)
    {
        // La Junta Municipal usa "Distrito Municipal Pantoja" para la misma localidad.
        if (string.Equals(texto, "Distrito Municipal Pantoja", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(texto, "Distrito Municipal de Pantoja", StringComparison.OrdinalIgnoreCase))
            return "Pantoja";

        return ElegibilidadRecogida.Zonas.FirstOrDefault(zona => zona != ElegibilidadRecogida.OtraZona &&
            zona.Equals(texto, StringComparison.OrdinalIgnoreCase));
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
