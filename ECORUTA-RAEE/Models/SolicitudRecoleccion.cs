
using SQLite;

namespace ECORUTA_RAEE.Models;

[Table("Solicitudes")]
public class SolicitudRecoleccion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Unique(Name = "UX_Solicitudes_CodigoSolicitud"), Collation("NOCASE")]
    public string CodigoSolicitud { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public string Zona { get; set; } = string.Empty;
    public string? SectorOtraZona { get; set; }

    public string DireccionReferencia { get; set; } = string.Empty;

    public double? Latitud { get; set; }

    public double? Longitud { get; set; }

    public string Estado { get; set; } = "Pendiente";

    public double PesoTotalEstimado { get; set; }
}
