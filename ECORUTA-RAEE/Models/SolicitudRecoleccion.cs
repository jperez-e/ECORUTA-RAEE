
using SQLite;

namespace ECORUTA_RAEE.Models;

[Table("Solicitudes")]
public class SolicitudRecoleccion
{
    public int? UsuarioId { get; set; }

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Unique(Name = "UX_Solicitudes_CodigoSolicitud"), Collation("NOCASE")]
    public string CodigoSolicitud { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public string Zona { get; set; } = string.Empty;
    public string? SectorOtraZona { get; set; }

    public string? NombreContacto { get; set; }
    public string? TelefonoContacto { get; set; }

    public string DireccionReferencia { get; set; } = string.Empty;

    public double? Latitud { get; set; }

    public double? Longitud { get; set; }

    public string Estado { get; set; } = "Pendiente";

    public double PesoTotalEstimado { get; set; }

    [Ignore] public bool EnJornadaActiva { get; internal set; }
    [Ignore] public bool UbicacionPendiente => !Coordenadas.SonValidas(this);
    [Ignore] public string DisponibilidadOperativa => EnJornadaActiva
        ? UbicacionPendiente ? "Incluida en jornada programada · Ubicación por completar" : "Incluida en jornada programada"
        : UbicacionPendiente ? "Ubicación por completar"
        : !ElegibilidadRecogida.ZonaAdmitida(Zona) ? "Zona pendiente de agrupación automática"
        : ElegibilidadRecogida.EsElegible(this) ? "Lista para recogida" : "Sin disponibilidad operativa";
}
