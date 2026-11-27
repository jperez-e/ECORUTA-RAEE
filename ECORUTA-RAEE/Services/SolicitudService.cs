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
        var db = await Database.GetConnectionAsync();
        SolicitudRecoleccion nueva = null!;
        await db.RunInTransactionAsync(connection => nueva = Insertar(connection, solicitud, numeroPersonalizado));
        CopiarIdentidad(nueva, solicitud);
    }

    private static SolicitudRecoleccion Insertar(
        SQLiteConnection connection, SolicitudRecoleccion solicitud, int? numeroPersonalizado)
    {
        ValidarUbicacion(solicitud);
        var nueva = new SolicitudRecoleccion
        {
            FechaRegistro = DateTime.Now,
            Zona = solicitud.Zona,
            SectorOtraZona = solicitud.SectorOtraZona,
            NombreContacto = solicitud.NombreContacto, TelefonoContacto = solicitud.TelefonoContacto,
            DireccionReferencia = solicitud.DireccionReferencia,
            Latitud = solicitud.Latitud,
            Longitud = solicitud.Longitud,
            Estado = solicitud.Estado,
            PesoTotalEstimado = solicitud.PesoTotalEstimado
        };

        long numero = numeroPersonalizado is int personalizado
            ? personalizado
            : ReservarNumero(connection);
        nueva.CodigoSolicitud = FormatearCodigo(numero);
        EjecutarEscrituraCodigo(connection, () => connection.Insert(nueva));

        if (numeroPersonalizado is int numeroManual)
            ActualizarSecuencia(connection, numeroManual);
        return nueva;
    }

    private static void CopiarIdentidad(SolicitudRecoleccion nueva, SolicitudRecoleccion solicitud)
    {
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
        await db.RunInTransactionAsync(connection => actualizado =
            Actualizar(connection, solicitud, actualizarPesoManual, codigo, numeroPersonalizado));
        if (actualizado && codigo is not null)
            solicitud.CodigoSolicitud = codigo;
        return actualizado;
    }

    private static bool Actualizar(SQLiteConnection connection, SolicitudRecoleccion solicitud,
        bool actualizarPesoManual, string? codigo, int? numeroPersonalizado)
    {
        ValidarUbicacion(solicitud);
        if (solicitud.UbicacionPendiente && connection.Find<SolicitudRecoleccion>(solicitud.Id) is { UbicacionPendiente: false } &&
            connection.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM JornadaSolicitudes WHERE SolicitudId = ? AND Activa = 1)",
                solicitud.Id) == 1)
            throw new UbicacionAsignadaException();
        // El peso manual solo se escribe si la solicitud sigue sin residuos.
        int actualizadas = EjecutarEscrituraCodigo(connection, () => connection.Execute("""
                UPDATE Solicitudes
                SET CodigoSolicitud = COALESCE(?, CodigoSolicitud),
                    Zona = ?, SectorOtraZona = ?, DireccionReferencia = ?, Latitud = ?, Longitud = ?,
                    NombreContacto = ?, TelefonoContacto = ?,
                    PesoTotalEstimado = CASE
                        WHEN ? AND NOT EXISTS (SELECT 1 FROM Residuos WHERE SolicitudId = Solicitudes.Id)
                        THEN ? ELSE PesoTotalEstimado END
                WHERE Id = ?
                """, codigo, solicitud.Zona, solicitud.SectorOtraZona, solicitud.DireccionReferencia, solicitud.Latitud,
                solicitud.Longitud, solicitud.NombreContacto, solicitud.TelefonoContacto, actualizarPesoManual, solicitud.PesoTotalEstimado, solicitud.Id));

        if (actualizadas == 1 && numeroPersonalizado is int numeroManual)
            ActualizarSecuencia(connection, numeroManual);
        return actualizadas == 1;
    }

    private static void ValidarUbicacion(SolicitudRecoleccion solicitud)
    {
        solicitud.NombreContacto = DatosContacto.NormalizarNombre(solicitud.NombreContacto);
        if (!DatosContacto.NombreValido(solicitud.NombreContacto) ||
            !DatosContacto.TryNormalizarTelefono(solicitud.TelefonoContacto, false, out var telefono))
            throw new ArgumentException("El contacto contiene datos inválidos.", nameof(solicitud));
        solicitud.TelefonoContacto = telefono;

        if ((solicitud.Latitud.HasValue || solicitud.Longitud.HasValue) && solicitud.UbicacionPendiente)
            throw new ArgumentException("La ubicación debe contener ambas coordenadas válidas.", nameof(solicitud));
    }

    internal async Task<bool> TieneAsignacionActivaAsync(int id)
    {
        var db = await Database.GetConnectionAsync();
        return await db.ExecuteScalarAsync<int>(
            "SELECT EXISTS (SELECT 1 FROM JornadaSolicitudes WHERE SolicitudId = ? AND Activa = 1)", id) != 0;
    }

    internal Task<bool> CompletarUbicacionAsync(SolicitudRecoleccion original, PuntoRecogida punto) =>
        CompletarUbicacionAsync(original, new SeleccionUbicacion(punto, original.Zona, original.SectorOtraZona));

    internal async Task<bool> CompletarUbicacionAsync(SolicitudRecoleccion original, SeleccionUbicacion ubicacion)
    {
        if (SeleccionUbicacion.Crear(ubicacion.Punto, ubicacion.Zona, ubicacion.Sector) is not { Punto: { } punto } seleccion)
            return false;

        var db = await Database.GetConnectionAsync();
        bool guardada = false;
        await db.RunInTransactionAsync(connection =>
        {
            var vigente = connection.Find<SolicitudRecoleccion>(original.Id);
            if (vigente is not { Estado: ElegibilidadRecogida.Pendiente, UbicacionPendiente: true } ||
                vigente.Zona != original.Zona || vigente.SectorOtraZona != original.SectorOtraZona ||
                vigente.Latitud != original.Latitud || vigente.Longitud != original.Longitud)
                return;
            if (vigente.Zona != seleccion.Zona && connection.ExecuteScalar<int>(
                "SELECT EXISTS (SELECT 1 FROM JornadaSolicitudes WHERE SolicitudId = ? AND Activa = 1)", vigente.Id) != 0)
                return;

            guardada = connection.Execute("""
                UPDATE Solicitudes SET Latitud = ?, Longitud = ?, Zona = ?, SectorOtraZona = ? WHERE Id = ?
                """, punto.Latitud, punto.Longitud, seleccion.Zona, seleccion.Sector, vigente.Id) == 1;
        });
        return guardada;
    }

    internal async Task<List<SolicitudRecoleccion>> ObtenerListadoOperativoAsync()
    {
        var db = await Database.GetConnectionAsync();
        List<SolicitudRecoleccion> solicitudes = [];
        await db.RunInTransactionAsync(connection =>
        {
            solicitudes = connection.Table<SolicitudRecoleccion>().OrderByDescending(s => s.FechaRegistro)
                .ThenByDescending(s => s.Id).ToList();
            var asignadas = connection.Query<AsignacionActiva>(
                "SELECT SolicitudId FROM JornadaSolicitudes WHERE Activa = 1").Select(a => a.SolicitudId).ToHashSet();
            foreach (var solicitud in solicitudes)
                solicitud.EnJornadaActiva = asignadas.Contains(solicitud.Id);
        });
        return solicitudes;
    }

    private sealed class AsignacionActiva
    {
        public int SolicitudId { get; set; }
    }

    internal async Task<(SolicitudRecoleccion? Solicitud, List<ResiduoElectronico> Equipos)> ObtenerCompletaAsync(int id)
    {
        var db = await Database.GetConnectionAsync();
        SolicitudRecoleccion? solicitud = null;
        List<ResiduoElectronico> equipos = new();
        await db.RunInTransactionAsync(connection =>
        {
            solicitud = connection.Find<SolicitudRecoleccion>(id);
            equipos = LeerEquipos(connection, id);
        });
        return (solicitud, equipos);
    }

    internal async Task<bool> GuardarSolicitudCompletaAsync(SolicitudRecoleccion solicitud,
        IReadOnlyList<ResiduoElectronico> borrador, IReadOnlyList<ResiduoElectronico> originales,
        SolicitudRecoleccion? original, int? numeroPersonalizado = null)
    {
        var ids = borrador.Where(e => e.Id != 0).Select(e => e.Id).ToHashSet();
        bool nueva = solicitud.Id == 0;
        if ((original?.Id ?? 0) != solicitud.Id ||
            borrador.Any(e => !DatosEquipo.EsValido(e)) ||
            ids.Count != borrador.Count(e => e.Id != 0) ||
            ids.Any(id => originales.All(e => e.Id != id)) ||
            (nueva && (borrador.Count == 0 || originales.Count != 0)) ||
            !double.IsFinite(borrador.Sum(e => e.PesoEstimado)))
            throw new InvalidOperationException("El borrador contiene equipos inválidos.");

        var db = await Database.GetConnectionAsync();
        SolicitudRecoleccion? insertada = null;
        bool encontrado = true;
        string? codigo = numeroPersonalizado is int numero ? FormatearCodigo(numero) : null;
        await db.RunInTransactionAsync(connection =>
        {
            var actual = nueva ? null : connection.Find<SolicitudRecoleccion>(solicitud.Id);
            if (!nueva && actual is null)
            {
                encontrado = false;
                return;
            }

            // Comparar la instantánea evita borrar o sobrescribir equipos modificados por otro flujo.
            var actuales = nueva ? new List<ResiduoElectronico>() : LeerEquipos(connection, solicitud.Id);
            if (!actuales.Select(ValoresEquipo).SequenceEqual(originales.Select(ValoresEquipo)))
                throw new EquiposSolicitudModificadosException();
            if (actual is not null && original is not null && ValoresSolicitud(actual) != ValoresSolicitud(original))
                throw new SolicitudModificadaException();

            bool pesoManual = actuales.Count == 0 && borrador.Count == 0;
            ValidarSolicitudCompleta(solicitud, pesoManual, actual?.PesoTotalEstimado);
            if (nueva)
                insertada = Insertar(connection, solicitud, numeroPersonalizado);
            else
                Actualizar(connection, solicitud, pesoManual, codigo, numeroPersonalizado);

            int id = insertada?.Id ?? solicitud.Id;
            foreach (var equipo in actuales.Where(e => !ids.Contains(e.Id)))
                connection.Delete(equipo);

            foreach (var equipo in borrador)
            {
                var fila = new ResiduoElectronico
                {
                    Id = equipo.Id, SolicitudId = id, Categoria = equipo.Categoria,
                    Descripcion = equipo.Descripcion, Cantidad = equipo.Cantidad, PesoEstimado = equipo.PesoEstimado
                };
                if (fila.Id == 0)
                    connection.Insert(fila);
                else
                    connection.Update(fila);
            }

            if (!pesoManual)
                ResiduoElectronicoService.SincronizarPesoSolicitud(connection, id);
        });

        if (insertada is not null)
            CopiarIdentidad(insertada, solicitud);
        else if (encontrado && codigo is not null)
            solicitud.CodigoSolicitud = codigo;
        return encontrado;
    }

    private static List<ResiduoElectronico> LeerEquipos(SQLiteConnection connection, int id) =>
        connection.Table<ResiduoElectronico>().Where(e => e.SolicitudId == id).OrderBy(e => e.Id).ToList();

    private static (int, int, string, string, int, double) ValoresEquipo(ResiduoElectronico equipo) =>
        (equipo.Id, equipo.SolicitudId, equipo.Categoria, equipo.Descripcion, equipo.Cantidad, equipo.PesoEstimado);

    private static (string, string, string?, string, double?, double?, string, double, string?, string?) ValoresSolicitud(
        SolicitudRecoleccion solicitud) =>
        (solicitud.CodigoSolicitud, solicitud.Zona, solicitud.SectorOtraZona, solicitud.DireccionReferencia,
            solicitud.Latitud, solicitud.Longitud, solicitud.Estado, solicitud.PesoTotalEstimado,
            solicitud.NombreContacto, solicitud.TelefonoContacto);

    private static void ValidarSolicitudCompleta(SolicitudRecoleccion solicitud, bool pesoManual, double? pesoActual)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Zona) || solicitud.DireccionReferencia.Trim().Length is < 5 or > 150 ||
            (solicitud.Zona.Equals(ElegibilidadRecogida.OtraZona, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(solicitud.SectorOtraZona) || solicitud.SectorOtraZona.Trim().Length > 100)) ||
            (pesoManual && (!double.IsFinite(solicitud.PesoTotalEstimado) ||
                solicitud.PesoTotalEstimado is < 0 or > 10000 ||
                (solicitud.PesoTotalEstimado == 0 && pesoActual != 0))))
            throw new InvalidOperationException("El borrador contiene una solicitud inválida.");
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

internal sealed class EquiposSolicitudModificadosException()
    : Exception("Los equipos cambiaron desde que se abrió el formulario.");

internal sealed class SolicitudModificadaException()
    : Exception("La solicitud cambió desde que se abrió el formulario.");

internal sealed class UbicacionAsignadaException()
    : Exception("Resuelve la jornada programada antes de quitar la ubicación.");
