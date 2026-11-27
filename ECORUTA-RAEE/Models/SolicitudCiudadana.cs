namespace ECORUTA_RAEE.Models;

internal sealed record SolicitudCiudadana(SolicitudRecoleccion Solicitud,
    IReadOnlyList<ResiduoElectronico> Equipos, DateTime? FechaProgramada = null)
{
    internal string EstadoVisual => FechaProgramada.HasValue ? "Programada"
        : Solicitud.UbicacionPendiente ? "Ubicación pendiente"
        : !ElegibilidadRecogida.ZonaAdmitida(Solicitud.Zona) ? "Fuera de agrupación"
        : Solicitud.Estado == ElegibilidadRecogida.Pendiente ? "En espera" : "Registrada";

    internal string ProximoPaso => FechaProgramada.HasValue
        ? Solicitud.UbicacionPendiente
            ? "Hay una fecha programada. Completa el punto de recogida."
            : "Consulta la fecha y la referencia de recogida."
        : !ElegibilidadRecogida.ZonaAdmitida(Solicitud.Zona)
            ? Solicitud.UbicacionPendiente
                ? "Completa el punto de recogida cuando tengas conexión. Esta zona está fuera de la agrupación automática actual."
                : "Esta zona está fuera de la agrupación automática actual."
            : Solicitud.UbicacionPendiente
                ? "Completa el punto de recogida. Puedes elegirlo en el mapa cuando tengas conexión."
                : "Tu ubicación está completa. Consulta aquí cuando se programe una jornada.";

    internal bool PuedeCompletarPunto =>
        Solicitud.UbicacionPendiente && Solicitud.Estado == ElegibilidadRecogida.Pendiente;

    internal string ZonaConSector => string.IsNullOrWhiteSpace(Solicitud.SectorOtraZona)
        ? Solicitud.Zona : $"{Solicitud.Zona} · {Solicitud.SectorOtraZona}";
}
