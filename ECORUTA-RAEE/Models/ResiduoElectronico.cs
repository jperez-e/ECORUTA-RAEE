
namespace ECORUTA_RAEE.Models;

public class ResiduoElectronico
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }

    public string Categoria { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public double PesoEstimado { get; set; }
}
