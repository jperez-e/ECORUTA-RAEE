using System.Globalization;
using ECORUTA_RAEE.Models;
using SQLite;

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

    public async Task CrearAsync(SolicitudRecoleccion solicitud, int? numeroPersonalizado = null)
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
            long numero = numeroPersonalizado is int personalizado
                ? personalizado
                : ReservarNumero(connection);
            nueva.CodigoSolicitud = FormatearCodigo(numero);
            EjecutarEscrituraCodigo(connection, () => connection.Insert(nueva));

            if (numeroPersonalizado is int numeroManual)
                ActualizarSecuencia(connection, numeroManual);
        });

        solicitud.Id = nueva.Id;
        solicitud.CodigoSolicitud = nueva.CodigoSolicitud;
        solicitud.FechaRegistro = nueva.FechaRegistro;
    }

    public async Task<bool> ActualizarAsync(
        SolicitudRecoleccion solicitud, bool actualizarPesoManual = true,
        int? numeroPersonalizado = null)
    {
        var db = await Database.GetConnectionAsync();
        string? codigo = numeroPersonalizado is int numero ? FormatearCodigo(numero) : null;
        bool actualizado = false;

        await db.RunInTransactionAsync(connection =>
        {
            // El peso manual solo se escribe si la solicitud sigue sin residuos.
            int actualizadas = EjecutarEscrituraCodigo(connection, () => connection.Execute("""
                UPDATE Solicitudes
                SET CodigoSolicitud = COALESCE(?, CodigoSolicitud),
                    Zona = ?, DireccionReferencia = ?,
                    PesoTotalEstimado = CASE
                        WHEN ? AND NOT EXISTS (SELECT 1 FROM Residuos WHERE SolicitudId = Solicitudes.Id)
                        THEN ? ELSE PesoTotalEstimado END
                WHERE Id = ?
                """, codigo, solicitud.Zona, solicitud.DireccionReferencia, actualizarPesoManual,
                solicitud.PesoTotalEstimado, solicitud.Id));

            actualizado = actualizadas == 1;
            if (actualizado && numeroPersonalizado is int numeroManual)
                ActualizarSecuencia(connection, numeroManual);
        });

        if (actualizado && codigo is not null)
            solicitud.CodigoSolicitud = codigo;

        return actualizado;
    }

    internal static bool TryLeerNumeroCodigo(string? texto, out int numero)
    {
        numero = 0;
        string codigo = texto?.Trim() ?? string.Empty;
        return codigo.StartsWith("SOL-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(codigo.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out numero)
            && numero > 0
            && codigo.Equals(FormatearCodigo(numero), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatearCodigo(long numero)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numero);
        return "SOL-" + numero.ToString("D4", CultureInfo.InvariantCulture);
    }

    private static long ReservarNumero(SQLiteConnection connection)
    {
        int actualizadas = connection.Execute("""
            UPDATE SecuenciaCodigoSolicitud
            SET UltimoNumero = UltimoNumero + 1
            WHERE Id = 1 AND UltimoNumero < 9223372036854775807
            """);
        if (actualizadas != 1)
            throw new InvalidOperationException("La secuencia de códigos no está disponible.");

        return connection.ExecuteScalar<long>(
            "SELECT UltimoNumero FROM SecuenciaCodigoSolicitud WHERE Id = 1");
    }

    private static void ActualizarSecuencia(SQLiteConnection connection, int numero)
    {
        int actualizadas = connection.Execute("""
            UPDATE SecuenciaCodigoSolicitud SET UltimoNumero = MAX(UltimoNumero, ?) WHERE Id = 1
            """, numero);
        if (actualizadas != 1)
            throw new InvalidOperationException("La secuencia de códigos no está disponible.");
    }

    private static int EjecutarEscrituraCodigo(SQLiteConnection connection, Func<int> escribir)
    {
        try
        {
            return escribir();
        }
        catch (SQLiteException ex) when (
            SQLite3.ExtendedErrCode(connection.Handle) == SQLite3.ExtendedResult.ConstraintUnique
            && ex.Message.EndsWith(": Solicitudes.CodigoSolicitud", StringComparison.Ordinal))
        {
            // Leer el código extendido antes de que el rollback cambie el estado de la conexión.
            throw new CodigoSolicitudDuplicadoException(ex);
        }
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

internal sealed class CodigoSolicitudDuplicadoException(SQLiteException innerException)
    : Exception("El código de solicitud ya está registrado.", innerException);
