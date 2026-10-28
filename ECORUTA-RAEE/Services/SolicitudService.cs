using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Services;

public class SolicitudService
{
    private readonly List<SolicitudRecoleccion> _solicitudes = new();

    private int _siguienteId = 1;

    // Instancia compartida durante la ejecución de la app.
    public static SolicitudService Instancia { get; } = new();

    private SolicitudService()
    {
    }

    public List<SolicitudRecoleccion> ObtenerTodas()
    {
        return _solicitudes
            .OrderByDescending(s => s.FechaRegistro)
            .ToList();
    }

    public SolicitudRecoleccion? ObtenerPorId(int id)
    {
        return _solicitudes.FirstOrDefault(s => s.Id == id);
    }

    public void Crear(SolicitudRecoleccion solicitud)
    {
        solicitud.Id = _siguienteId++;
        solicitud.CodigoSolicitud = $"SOL-{solicitud.Id:0000}";
        solicitud.FechaRegistro = DateTime.Now;

        _solicitudes.Add(solicitud);
    }

    public int Contar()
    {
        return _solicitudes.Count;
    }
}
