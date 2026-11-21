using System.Globalization;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Services;

public class SolicitudService
{
    public static SolicitudService Instancia { get; } = new();
    private EcoRutaDatabase Database => EcoRutaDatabase.Instance;

    private SolicitudService() { }

    public async Task<List<SolicitudRecoleccion>> ObtenerTodasAsync()
    {
        var db = await Database.GetConnectionAsync();
        return await db.Table<SolicitudRecoleccion>()
            .OrderByDescending(s => s.FechaRegistro)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
    }

    public async Task<SolicitudRecoleccion?> ObtenerPorIdAsync(int id)
    {
        var db = await Database.GetConnectionAsync();
        return await db.FindAsync<SolicitudRecoleccion>(id);
    }

    public async Task CrearAsync(SolicitudRecoleccion solicitud)
    {
        var nueva = new SolicitudRecoleccion
        {
            FechaRegistro = DateTime.Now,
            Zona = solicitud.Zona,
            DireccionReferencia = solicitud.DireccionReferencia,
            Latitud = solicitud.Latitud,
            Longitud = solicitud.Longitud,
            Estado = solicitud.Estado,
            PesoTotalEstimado = solicitud.PesoTotalEstimado
        };

        var db = await Database.GetConnectionAsync();
        await db.RunInTransactionAsync(connection =>
        {
            int actualizadas = connection.Execute("""
                UPDATE SecuenciaCodigoSolicitud
                SET UltimoNumero = UltimoNumero + 1
                WHERE Id = 1 AND UltimoNumero < 9223372036854775807
                """);
            if (actualizadas != 1)
                throw new InvalidOperationException("La secuencia de códigos no está disponible.");

            long numero = connection.ExecuteScalar<long>(
                "SELECT UltimoNumero FROM SecuenciaCodigoSolicitud WHERE Id = 1");
            nueva.CodigoSolicitud = "SOL-" + numero.ToString("D4", CultureInfo.InvariantCulture);
            connection.Insert(nueva);
        });

        solicitud.Id = nueva.Id;
        solicitud.CodigoSolicitud = nueva.CodigoSolicitud;
        solicitud.FechaRegistro = nueva.FechaRegistro;
    }

    public async Task<bool> ActualizarAsync(
        SolicitudRecoleccion solicitud, bool actualizarPesoManual = true)
    {
        var db = await Database.GetConnectionAsync();

        // Una sola sentencia mantiene el peso actual si hay residuos o si
        // el formulario presentó un peso calculado en lugar de un valor manual.
        int actualizadas = await db.ExecuteAsync("""
            UPDATE Solicitudes
            SET Zona = ?, DireccionReferencia = ?,
                PesoTotalEstimado = CASE
                    WHEN ? AND NOT EXISTS (SELECT 1 FROM Residuos WHERE SolicitudId = Solicitudes.Id)
                    THEN ? ELSE PesoTotalEstimado END
            WHERE Id = ?
            """, solicitud.Zona, solicitud.DireccionReferencia, actualizarPesoManual,
            solicitud.PesoTotalEstimado, solicitud.Id);
        return actualizadas == 1;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var db = await Database.GetConnectionAsync();
        // SQLite elimina los residuos asociados dentro de la misma sentencia.
        return await db.DeleteAsync<SolicitudRecoleccion>(id) == 1;
    }

    public async Task<int> ContarAsync()
    {
        var db = await Database.GetConnectionAsync();
        return await db.Table<SolicitudRecoleccion>().CountAsync();
    }
}
