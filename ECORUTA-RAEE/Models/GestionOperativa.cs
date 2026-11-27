namespace ECORUTA_RAEE.Models;

internal enum FiltroGestion { Todas, Disponibles, Programadas, SinUbicacion, OtraZona, ProgramadasSinTelefono }

public sealed class SolicitudGestion : SolicitudRecoleccion
{
    public long Unidades { get; set; }
    public DateTime? FechaProgramada { get; set; }
    public bool TieneTelefono { get; set; }
    public bool TieneFecha => FechaProgramada.HasValue;
    public string ContactoVisible => TieneTelefono ? "Teléfono disponible" : "Sin teléfono registrado";
    public string ZonaVisible => string.IsNullOrWhiteSpace(SectorOtraZona) ? Zona : $"{Zona} · {SectorOtraZona}";
    public string EstadoGestion => EnJornadaActiva ? "Programada" : UbicacionPendiente ? "Ubicación pendiente"
        : Zona == ElegibilidadRecogida.OtraZona ? "Otra zona"
        : ElegibilidadRecogida.EsElegible(this) ? "Disponible para jornada" : Estado;

    internal bool Coincide(FiltroGestion filtro) => filtro switch
    {
        FiltroGestion.Disponibles => ElegibilidadRecogida.EsElegible(this),
        FiltroGestion.Programadas => EnJornadaActiva,
        FiltroGestion.SinUbicacion => UbicacionPendiente,
        FiltroGestion.OtraZona => Zona.Equals(ElegibilidadRecogida.OtraZona, StringComparison.OrdinalIgnoreCase),
        FiltroGestion.ProgramadasSinTelefono => EnJornadaActiva && !TieneTelefono,
        _ => true
    };
}

internal sealed record AlertaGestion(string Titulo, string Detalle, string Accion, string Icono,
    string? Zona = null, int? JornadaId = null, FiltroGestion? Filtro = null)
{
    internal const string RutaSolicitudes = "//solicitudes";
    internal const string RutaJornada = "JornadaDetallePage";
    internal string Ruta => Filtro.HasValue ? RutaSolicitudes : RutaJornada;
    internal string Clave => Filtro.HasValue ? "Filtro" : JornadaId.HasValue ? "JornadaId" : "Zona";
    internal object? Valor => Filtro.HasValue ? Filtro.Value : JornadaId.HasValue ? JornadaId.Value : Zona;

    internal static IReadOnlyList<AlertaGestion> Crear(PanelAmbiental panel, IReadOnlyList<ResumenJornadaCampo> jornadas)
    {
        var alertas = panel.Alertas.Where(z => z.EsLista).Select(z =>
            new AlertaGestion($"{z.Zona} está lista", z.DetalleAlerta, "Programar jornada", "listo", Zona: z.Zona)).ToList();
        foreach (var jornada in jornadas.Where(j => j.Orden != EstadoOrdenCampo.Vigente || j.UbicacionesIncompletas > 0))
        {
            var detalles = new List<string>();
            if (jornada.Orden != EstadoOrdenCampo.Vigente)
                detalles.Add(jornada.EstadoOrden);
            if (jornada.UbicacionesIncompletas > 0)
                detalles.Add($"{jornada.UbicacionesIncompletas} sin punto");
            alertas.Add(new($"{jornada.Zona} · {jornada.Fecha:dd/MM/yyyy}", string.Join(" · ", detalles),
                "Revisar jornada", "punto", JornadaId: jornada.Id));
        }
        if (panel.UbicacionesPendientes > 0)
            alertas.Add(new("Ubicaciones por completar", $"{panel.UbicacionesPendientes} solicitudes pendientes sin punto.",
                "Ver solicitudes", "punto", Filtro: FiltroGestion.SinUbicacion));
        alertas.AddRange(panel.Alertas.Where(z => z.Nivel == NivelAlertaZona.Proxima).Select(z =>
            new AlertaGestion($"{z.Zona} está próxima al umbral", z.DetalleAlerta, "Ver disponibilidad", "reloj", Zona: z.Zona)));
        int sinTelefono = jornadas.Sum(j => j.SinTelefono);
        if (sinTelefono > 0)
            alertas.Add(new("Solicitudes programadas sin teléfono", $"{sinTelefono} paradas requieren coordinar por otros medios.",
                "Ver solicitudes programadas", "equipo", Filtro: FiltroGestion.ProgramadasSinTelefono));
        return alertas;
    }
}
