namespace ECORUTA_RAEE.Models;

internal static class ElegibilidadRecogida
{
    internal const string Pendiente = "Pendiente";
    internal const string OtraZona = "Otra zona";
    internal static IReadOnlyList<string> Zonas { get; } =
        Array.AsReadOnly(new[] { "Herrera", "Los Alcarrizos", "Manoguayabo", "Pantoja", "Pedro Brand", OtraZona });

    internal static bool ZonaAdmitida(string zona) =>
        !string.Equals(zona?.Trim(), OtraZona, StringComparison.OrdinalIgnoreCase) &&
        Zonas.Contains(zona?.Trim() ?? "", StringComparer.OrdinalIgnoreCase);

    internal static bool EsElegible(SolicitudRecoleccion solicitud) =>
        solicitud.Estado == Pendiente && !solicitud.EnJornadaActiva &&
        Coordenadas.SonValidas(solicitud) && ZonaAdmitida(solicitud.Zona);
}
