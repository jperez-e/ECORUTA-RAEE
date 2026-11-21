namespace ECORUTA_RAEE.Models;

internal enum MetodoRuta
{
    VecinoMasCercano = 1,
    Antiguedad = 2
}

internal sealed record ParadaRuta(SolicitudRecoleccion Solicitud, int Orden, double? DistanciaKm)
{
    public string Titulo => Orden > 0 ? $"{Orden}. {Solicitud.CodigoSolicitud}" : Solicitud.CodigoSolicitud;
    public bool TieneDistancia => DistanciaKm.HasValue;
}

internal sealed record PlanificacionRuta(MetodoRuta Metodo, IReadOnlyList<ParadaRuta> Paradas);

internal static class Coordenadas
{
    public const double LimiteLatitud = 90;
    public const double LimiteLongitud = 180;

    public static bool EnRango(double? valor, double limite) =>
        valor is double numero && double.IsFinite(numero) && Math.Abs(numero) <= limite;

    public static bool SonValidas(SolicitudRecoleccion solicitud) =>
        EnRango(solicitud.Latitud, LimiteLatitud) && EnRango(solicitud.Longitud, LimiteLongitud);
}
