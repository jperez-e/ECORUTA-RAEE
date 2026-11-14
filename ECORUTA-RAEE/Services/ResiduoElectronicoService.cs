
using System;
using System.Collections.Generic;
using System.Linq;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Services;

public class ResiduoElectronicoService
{
    // Almacenamiento temporal en memoria.
    private readonly List<ResiduoElectronico> _residuos = new();

    // Identificador automático.
    private int _siguienteId = 1;

    // Instancia única del servicio.
    public static ResiduoElectronicoService Instancia { get; } = new();

    private ResiduoElectronicoService()
    {
    }

    // READ: Obtener todos los residuos.
    public List<ResiduoElectronico> ObtenerTodos()
    {
        return _residuos
            .OrderByDescending(r => r.Id)
            .ToList();
    }

    // READ: Obtener un residuo por ID.
    public ResiduoElectronico? ObtenerPorId(int id)
    {
        return _residuos.FirstOrDefault(r => r.Id == id);
    }

    // READ: Obtener los residuos de una solicitud.
    public List<ResiduoElectronico> ObtenerPorSolicitudId(int solicitudId)
    {
        return _residuos
            .Where(r => r.SolicitudId == solicitudId)
            .ToList();
    }

    // Calcular el peso total de los residuos de una solicitud.
    // PesoEstimado ya representa el peso total del registro,
    // por lo que NO se multiplica nuevamente por Cantidad.
    public double CalcularPesoTotalPorSolicitud(int solicitudId)
    {
        return _residuos
            .Where(r => r.SolicitudId == solicitudId)
            .Sum(r => r.PesoEstimado);
    }

    // CREATE: Registrar un residuo electrónico.
    public bool Crear(ResiduoElectronico residuo)
    {
        if (!EsValido(residuo))
            return false;

        residuo.Id = _siguienteId++;
        _residuos.Add(residuo);

        SincronizarPesoSolicitud(residuo.SolicitudId);

        return true;
    }

    // UPDATE: Actualizar un residuo existente.
    public bool Actualizar(ResiduoElectronico residuo)
    {
        if (!EsValido(residuo))
            return false;

        var existente = ObtenerPorId(residuo.Id);

        if (existente == null)
            return false;

        // Guardar la solicitud anterior por si cambia la asociación.
        int solicitudAnteriorId = existente.SolicitudId;

        existente.SolicitudId = residuo.SolicitudId;
        existente.Categoria = residuo.Categoria;
        existente.Descripcion = residuo.Descripcion;
        existente.Cantidad = residuo.Cantidad;
        existente.PesoEstimado = residuo.PesoEstimado;

        // Actualizar el total de la solicitud nueva o actual.
        SincronizarPesoSolicitud(existente.SolicitudId);

        // Si cambió de solicitud, actualizar también la anterior.
        if (solicitudAnteriorId != existente.SolicitudId)
        {
            SincronizarPesoSolicitud(solicitudAnteriorId);
        }

        return true;
    }

    // DELETE: Eliminar un residuo por ID.
    public bool Eliminar(int id)
    {
        var residuo = ObtenerPorId(id);

        if (residuo == null)
            return false;

        int solicitudId = residuo.SolicitudId;

        bool eliminado = _residuos.Remove(residuo);

        if (eliminado)
        {
            SincronizarPesoSolicitud(solicitudId);
        }

        return eliminado;
    }

    // Contar todos los residuos.
    public int Contar()
    {
        return _residuos.Count;
    }

    // Eliminar todos los residuos de una solicitud.
    public int EliminarPorSolicitudId(int solicitudId)
    {
        int eliminados = _residuos.RemoveAll(
            r => r.SolicitudId == solicitudId);

        if (eliminados > 0)
        {
            SincronizarPesoSolicitud(solicitudId);
        }

        return eliminados;
    }

    // Mantener sincronizado el peso en la entidad principal.
    private void SincronizarPesoSolicitud(int solicitudId)
    {
        var solicitud = SolicitudService.Instancia
            .ObtenerPorId(solicitudId);

        if (solicitud == null)
            return;

        // Si se eliminó el último residuo, el resultado es 0.
        solicitud.PesoTotalEstimado =
            CalcularPesoTotalPorSolicitud(solicitudId);
    }

    // Validación básica de los datos.
    private static bool EsValido(ResiduoElectronico residuo)
    {
        if (residuo == null)
            return false;

        // La solicitud asociada debe existir.
        if (SolicitudService.Instancia
            .ObtenerPorId(residuo.SolicitudId) == null)
            return false;

        if (string.IsNullOrWhiteSpace(residuo.Categoria))
            return false;

        if (string.IsNullOrWhiteSpace(residuo.Descripcion))
            return false;

        if (residuo.Cantidad <= 0)
            return false;

        if (double.IsNaN(residuo.PesoEstimado) ||
            double.IsInfinity(residuo.PesoEstimado) ||
            residuo.PesoEstimado <= 0)
            return false;

        return true;
    }
}
