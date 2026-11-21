using ECORUTA_RAEE.Models;
using SQLite;

namespace ECORUTA_RAEE.Services;

public class ResiduoElectronicoService
{
    public static ResiduoElectronicoService Instancia { get; } = new();
    private EcoRutaDatabase Database => EcoRutaDatabase.Instance;

    private ResiduoElectronicoService() { }

    public async Task<List<ResiduoElectronico>> ObtenerTodosAsync()
    {
        var db = await Database.GetConnectionAsync();
        return await db.Table<ResiduoElectronico>().OrderByDescending(r => r.Id).ToListAsync();
    }

    internal async Task<(List<ResiduoElectronico> Residuos, Dictionary<int, string> Codigos)> ObtenerListadoAsync()
    {
        var db = await Database.GetConnectionAsync();
        List<ResiduoElectronico> residuos = new();
        Dictionary<int, string> codigos = new();

        await db.RunInTransactionAsync(connection =>
        {
            residuos = connection.Table<ResiduoElectronico>().OrderByDescending(r => r.Id).ToList();
            codigos = connection.Table<SolicitudRecoleccion>()
                .ToDictionary(s => s.Id, s => s.CodigoSolicitud);
        });

        return (residuos, codigos);
    }

    public async Task<ResiduoElectronico?> ObtenerPorIdAsync(int id)
    {
        var db = await Database.GetConnectionAsync();
        return await db.FindAsync<ResiduoElectronico>(id);
    }

    public async Task<List<ResiduoElectronico>> ObtenerPorSolicitudIdAsync(int solicitudId)
    {
        var db = await Database.GetConnectionAsync();
        return await db.Table<ResiduoElectronico>()
            .Where(r => r.SolicitudId == solicitudId)
            .OrderBy(r => r.Id)
            .ToListAsync();
    }

    public async Task<int> ContarPorSolicitudIdAsync(int solicitudId)
    {
        var db = await Database.GetConnectionAsync();
        return await db.Table<ResiduoElectronico>()
            .Where(r => r.SolicitudId == solicitudId)
            .CountAsync();
    }

    public async Task<bool> CrearAsync(ResiduoElectronico residuo)
    {
        if (!EsValido(residuo))
            return false;

        var db = await Database.GetConnectionAsync();
        int id = 0;
        await db.RunInTransactionAsync(connection =>
        {
            connection.Execute("""
                INSERT INTO Residuos (SolicitudId, Categoria, Descripcion, Cantidad, PesoEstimado)
                VALUES (?, ?, ?, ?, ?)
                """, residuo.SolicitudId, residuo.Categoria, residuo.Descripcion,
                residuo.Cantidad, residuo.PesoEstimado);
            id = connection.ExecuteScalar<int>("SELECT last_insert_rowid()");
            SincronizarPesoSolicitud(connection, residuo.SolicitudId);
        });

        residuo.Id = id;
        return true;
    }

    public async Task<bool> ActualizarAsync(ResiduoElectronico residuo)
    {
        if (!EsValido(residuo))
            return false;

        var db = await Database.GetConnectionAsync();
        bool actualizado = false;
        await db.RunInTransactionAsync(connection =>
        {
            var anterior = connection.Find<ResiduoElectronico>(residuo.Id);
            if (anterior is null)
                return;

            connection.Update(residuo);
            SincronizarPesoSolicitud(connection, residuo.SolicitudId);
            if (anterior.SolicitudId != residuo.SolicitudId)
                SincronizarPesoSolicitud(connection, anterior.SolicitudId);
            actualizado = true;
        });

        return actualizado;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var db = await Database.GetConnectionAsync();
        bool eliminado = false;
        await db.RunInTransactionAsync(connection =>
        {
            var residuo = connection.Find<ResiduoElectronico>(id);
            if (residuo is null)
                return;

            connection.Delete(residuo);
            SincronizarPesoSolicitud(connection, residuo.SolicitudId);
            eliminado = true;
        });

        return eliminado;
    }

    private static void SincronizarPesoSolicitud(SQLiteConnection connection, int solicitudId)
    {
        // PesoEstimado representa el total de la fila; SUM da NULL si no quedan filas.
        connection.Execute("""
            UPDATE Solicitudes
            SET PesoTotalEstimado = (
                SELECT COALESCE(SUM(PesoEstimado), 0) FROM Residuos WHERE SolicitudId = ?
            )
            WHERE Id = ?
            """, solicitudId, solicitudId);
    }

    private static bool EsValido(ResiduoElectronico residuo) =>
        residuo is not null &&
        !string.IsNullOrWhiteSpace(residuo.Categoria) &&
        !string.IsNullOrWhiteSpace(residuo.Descripcion) &&
        residuo.Cantidad > 0 &&
        double.IsFinite(residuo.PesoEstimado) &&
        residuo.PesoEstimado > 0;
}
