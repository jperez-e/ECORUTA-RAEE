using System.Globalization;
using System.Text.Json;

namespace ECORUTA_RAEE.Models;

internal sealed record PuntoRecogida
{
    private PuntoRecogida(double latitud, double longitud) =>
        (Latitud, Longitud) = (latitud, longitud);

    public double Latitud { get; }
    public double Longitud { get; }

    public static PuntoRecogida? Crear(double latitud, double longitud) =>
        Coordenadas.EnRango(latitud, Coordenadas.LimiteLatitud) &&
        Coordenadas.EnRango(longitud, Coordenadas.LimiteLongitud)
            ? new PuntoRecogida(latitud, longitud) : null;

    public static PuntoRecogida? LeerSeleccion(string? mensaje, string sesion)
    {
        if (string.IsNullOrEmpty(mensaje) || mensaje.Length > 256)
            return null;

        if (mensaje[0] == '"')
        {
            try { mensaje = JsonSerializer.Deserialize<string>(mensaje); }
            catch (JsonException) { return null; }
        }

        var datos = mensaje?.Split('|');
        if (datos is not { Length: 3 } || !string.Equals(datos[0], sesion, StringComparison.Ordinal) ||
            !double.TryParse(datos[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitud) ||
            !double.TryParse(datos[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitud))
            return null;

        return Crear(latitud, longitud);
    }
}
