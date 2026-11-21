
using SQLite;

namespace ECORUTA_RAEE.Models;

[Table("Residuos")]
public
 class ResiduoElectronico
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int SolicitudId { get; set; }

    public string Categoria { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public double PesoEstimado { get; set; }
}
