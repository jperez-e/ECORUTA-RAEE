namespace ECORUTA_RAEE.Models;

internal sealed record IndicadoresAmbientales(
    int Solicitudes, int Pendientes, double PesoVigente, long RegistrosResiduos, long UnidadesEquipos);

internal sealed class ResumenJornadasAmbiental
{
    public int Programadas { get; set; }
    public int Canceladas { get; set; }
    public int SolicitudesAsignadas { get; set; }
}

internal enum NivelAlertaZona { SinAlerta, Proxima, Lista }

internal sealed record ZonaAmbiental(
    string Zona, int Cantidad, double Peso, double ProporcionPeso, ZonaDisponible Disponibilidad)
{
    private const double MinimoProximidad = 0.75;

    public int PendientesDisponibles => Disponibilidad.Cantidad;
    public double PesoDisponible => Disponibilidad.Peso;
    public double Avance => Math.Clamp(Math.Max(
        Disponibilidad.Umbrales.Cantidad > 0 ? (double)PendientesDisponibles / Disponibilidad.Umbrales.Cantidad : 0,
        Disponibilidad.Umbrales.Peso > 0 ? PesoDisponible / Disponibilidad.Umbrales.Peso : 0), 0, 1);
    public NivelAlertaZona Nivel => PendientesDisponibles == 0 ? NivelAlertaZona.SinAlerta
        : Disponibilidad.AlcanzaUmbral ? NivelAlertaZona.Lista
        : Avance >= MinimoProximidad ? NivelAlertaZona.Proxima : NivelAlertaZona.SinAlerta;

    public bool EsLista => Nivel == NivelAlertaZona.Lista;
    public string EstadoAlerta => Nivel switch
    {
        NivelAlertaZona.Lista => "Lista para generar jornada",
        NivelAlertaZona.Proxima => "Próxima al umbral",
        _ => "Sin alerta"
    };

    public string DetalleAlerta => EsLista
        ? $"{PendientesDisponibles} disponibles · {PesoDisponible:0.##} kg"
        : Disponibilidad.EstadoUmbral;
}

internal sealed class CategoriaAmbiental
{
    public string Categoria { get; set; } = string.Empty;
    public long Registros { get; set; }
    public long Unidades { get; set; }
    public double Peso { get; set; }
    public double ProporcionEquipos { get; set; }
}

internal sealed record PanelAmbiental(
    IndicadoresAmbientales Indicadores, ResumenJornadasAmbiental Jornadas, UmbralesJornada Umbrales,
    IReadOnlyList<ZonaAmbiental> Zonas, IReadOnlyList<ZonaAmbiental> Alertas,
    IReadOnlyList<CategoriaAmbiental> Categorias, int SolicitudesSinZona, double PesoSinZona,
    int UbicacionesPendientes, int CantidadElegible, double PesoElegible)
{
    public bool HaySolicitudesSinZona => SolicitudesSinZona > 0;
    public bool SinSolicitudes => Indicadores.Solicitudes == 0;
}
