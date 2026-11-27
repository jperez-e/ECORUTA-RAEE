using SQLite;

namespace ECORUTA_RAEE.Models;

[Table("Jornadas")]
public class JornadaRecoleccion
{
    public const string Programada = "Programada";
    public const string Cancelada = "Cancelada";

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Zona { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaProgramada { get; set; }
    public string Estado { get; set; } = Programada;

    [Ignore]
    public string Nombre => $"Jornada · {Zona}";

    [Ignore]
    public IReadOnlyList<SolicitudRecoleccion> Solicitudes { get; internal set; } = Array.Empty<SolicitudRecoleccion>();

    [Ignore]
    public int CantidadSolicitudes => Solicitudes.Count;

    [Ignore] public long UnidadesEquipos { get; internal set; }

    [Ignore]
    public double PesoTotal => Solicitudes.Sum(solicitud => solicitud.PesoTotalEstimado);

    [Ignore]
    internal PlanificacionRuta? Ruta { get; set; }

    [Ignore]
    internal bool RutaDesactualizada { get; set; }
}
