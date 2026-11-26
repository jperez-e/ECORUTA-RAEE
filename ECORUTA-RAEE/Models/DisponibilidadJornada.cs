namespace ECORUTA_RAEE.Models;

internal sealed record UmbralesJornada(int Cantidad, double Peso);

internal sealed class TotalesSolicitudesZona
{
    public string Zona { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int Pendientes { get; set; }
    public double Peso { get; set; }
}

internal sealed record ZonaDisponible(
    string Zona, int Cantidad, double Peso, UmbralesJornada Umbrales)
{
    public int FaltanSolicitudes => Math.Max(0, Umbrales.Cantidad - Cantidad);
    public double FaltanKilogramos => Math.Max(0, Umbrales.Peso - Peso);
    public bool AlcanzaUmbral => FaltanSolicitudes == 0 || FaltanKilogramos == 0;
    public string EstadoUmbral => AlcanzaUmbral
        ? "Alcanza el umbral"
        : $"Faltan {FaltanSolicitudes} solicitudes o {FaltanKilogramos:0.##} kg";
}

internal sealed record PanelJornadas(
    UmbralesJornada Umbrales,
    IReadOnlyList<ZonaDisponible> Zonas,
    IReadOnlyList<JornadaRecoleccion> Jornadas,
    int SinZonaEspecifica, int UbicacionesPendientes);

internal sealed record VistaPreviaJornada(
    ZonaDisponible Disponibilidad,
    IReadOnlyList<SolicitudRecoleccion> Solicitudes);
