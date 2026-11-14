
using System;
using System.Collections.Generic;
using System.Linq;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Services;

public class SolicitudService
{
    private readonly List<SolicitudRecoleccion> _solicitudes = new();
    private int _siguienteId = 1;

    public static SolicitudService Instancia { get; } = new();

    private SolicitudService()
    {
    }

    // READ: Obtener todas las solicitudes.
    public List<SolicitudRecoleccion> ObtenerTodas()
    {
        return _solicitudes
            .OrderByDescending(s => s.FechaRegistro)
            .ToList();
    }

    // READ: Obtener una solicitud por ID.
    public SolicitudRecoleccion? ObtenerPorId(int id)
    {
        return _solicitudes.FirstOrDefault(s => s.Id == id);
    }

    // CREATE: Registrar una nueva solicitud.
    public void Crear(SolicitudRecoleccion solicitud)
    {
        solicitud.Id = _siguienteId++;
        solicitud.CodigoSolicitud = $"SOL-{solicitud.Id:0000}";
        solicitud.FechaRegistro = DateTime.Now;

        // Inicialmente, sin residuos, se conserva el peso manual.
        _solicitudes.Add(solicitud);
    }

    // UPDATE: Actualizar sin sobrescribir el peso calculado.
    public bool Actualizar(SolicitudRecoleccion solicitud)
    {
        var existente = ObtenerPorId(solicitud.Id);

        if (existente == null)
            return false;

        existente.Zona = solicitud.Zona;
        existente.DireccionReferencia = solicitud.DireccionReferencia;

        var servicioResiduos = ResiduoElectronicoService.Instancia;

        bool tieneResiduos = servicioResiduos
            .ObtenerPorSolicitudId(existente.Id).Count > 0;

        if (tieneResiduos)
        {
            // El peso lo determinan los residuos asociados.
            existente.PesoTotalEstimado = servicioResiduos
                .CalcularPesoTotalPorSolicitud(existente.Id);
        }
        else
        {
            // Sin residuos, se permite editar el peso manualmente.
            existente.PesoTotalEstimado = solicitud.PesoTotalEstimado;
        }

        return true;
    }

    // DELETE: Eliminar una solicitud y sus residuos asociados.
    public bool Eliminar(int id)
    {
        var solicitud = ObtenerPorId(id);

        if (solicitud == null)
            return false;

        // Evita dejar residuos huérfanos.
        ResiduoElectronicoService.Instancia
            .EliminarPorSolicitudId(id);

        return _solicitudes.Remove(solicitud);
    }

    public int Contar() => _solicitudes.Count;
}
