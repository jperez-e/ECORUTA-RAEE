using System;
using System.Collections.Generic;
using System.Text;

namespace ECORUTA_RAEE.Models;

public class SolicitudRecoleccion
{
    public int Id { get; set; }

    public string CodigoSolicitud { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public string Zona { get; set; } = string.Empty;

    public string DireccionReferencia { get; set; } = string.Empty;

    public double? Latitud { get; set; }

    public double? Longitud { get; set; }

    public string Estado { get; set; } = "Pendiente";

    public double PesoTotalEstimado { get; set; }
}
