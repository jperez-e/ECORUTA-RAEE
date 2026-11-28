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
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        return await db.Table<SolicitudRecoleccion>()
            .OrderByDescending(s => s.FechaRegistro)
            .ThenByDescending(s => s.Id)
            .ToListAsync();
    }

    public async Task<SolicitudRecoleccion?> ObtenerPorIdAsync(int id)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        return await db.FindAsync<SolicitudRecoleccion>(id);
    }

    public async Task CrearAsync(SolicitudRecoleccion solicitud, int? numeroPersonalizado = null)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        SolicitudRecoleccion nueva = null!;
        await db.RunInTransactionAsync(connection => nueva = Insertar(connection, solicitud, numeroPersonalizado));
        CopiarIdentidad(nueva, solicitud);
    }

    private static SolicitudRecoleccion Insertar(
        SQLiteConnection connection, SolicitudRecoleccion solicitud, int? numeroPersonalizado, int? propietario = null)
    {
        ValidarUbicacion(solicitud);
        var nueva = new SolicitudRecoleccion
        {
            FechaRegistro = DateTime.Now,
            UsuarioId = propietario,
            Zona = solicitud.Zona,
            SectorOtraZona = solicitud.SectorOtraZona,
            NombreContacto = solicitud.NombreContacto, TelefonoContacto = solicitud.TelefonoContacto,
            DireccionReferencia = solicitud.DireccionReferencia,
            Latitud = solicitud.Latitud,
            Longitud = solicitud.Longitud,
            Estado = propietario.HasValue ? ElegibilidadRecogida.Pendiente : solicitud.Estado,
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
        solicitud.UsuarioId = nueva.UsuarioId;
        solicitud.CodigoSolicitud = nueva.CodigoSolicitud;
        solicitud.FechaRegistro = nueva.FechaRegistro;
    }

    public async Task<bool> ActualizarAsync(
        SolicitudRecoleccion solicitud, bool actualizarPesoManual = true,
        int? numeroPersonalizado = null)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
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
        var actor = Autenticacion.Instancia.Exigir(RolUsuario.Ciudadano, RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        return await db.ExecuteScalarAsync<int>(
            "SELECT EXISTS (SELECT 1 FROM JornadaSolicitudes js JOIN Solicitudes s ON s.Id = js.SolicitudId WHERE js.SolicitudId = ? AND js.Activa = 1 AND (? IS NULL OR s.UsuarioId = ?))",
            id, Propietario(actor), Propietario(actor)) != 0;
    }

    internal Task<bool> CompletarUbicacionAsync(SolicitudRecoleccion original, PuntoRecogida punto) =>
        CompletarUbicacionAsync(original, new SeleccionUbicacion(punto, original.Zona, original.SectorOtraZona));

    internal async Task<bool> CompletarUbicacionAsync(SolicitudRecoleccion original, SeleccionUbicacion ubicacion)
    {
        var actor = Autenticacion.Instancia.Exigir(RolUsuario.Ciudadano, RolUsuario.Gestor);
        if (SeleccionUbicacion.Crear(ubicacion.Punto, ubicacion.Zona, ubicacion.Sector) is not { Punto: { } punto } seleccion)
            return false;

        var db = await Database.GetConnectionAsync();
        bool guardada = false;
        await db.RunInTransactionAsync(connection =>
        {
            var vigente = connection.Find<SolicitudRecoleccion>(original.Id);
            if (vigente is null || !PuedeConsultar(actor, vigente) || vigente is not { Estado: ElegibilidadRecogida.Pendiente, UbicacionPendiente: true } ||
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
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
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

    internal async Task<IReadOnlyList<SolicitudGestion>> ObtenerGestionAsync()
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        var filas = await db.QueryAsync<SolicitudGestion>("""
            SELECT s.Id, s.CodigoSolicitud, s.FechaRegistro, s.Zona, s.SectorOtraZona, s.DireccionReferencia,
                   s.Latitud, s.Longitud, s.Estado, s.PesoTotalEstimado, j.FechaProgramada,
                   (length(trim(COALESCE(s.TelefonoContacto, ''))) > 0) AS TieneTelefono,
                   COALESCE(r.Unidades, 0) AS Unidades
            FROM Solicitudes s
            LEFT JOIN JornadaSolicitudes js ON js.SolicitudId = s.Id AND js.Activa = 1
            LEFT JOIN Jornadas j ON j.Id = js.JornadaId AND j.Estado = ?
            LEFT JOIN (SELECT SolicitudId, SUM(Cantidad) AS Unidades FROM Residuos GROUP BY SolicitudId) r
                ON r.SolicitudId = s.Id
            ORDER BY s.FechaRegistro DESC, s.Id DESC
            """, JornadaRecoleccion.Programada);
        foreach (var fila in filas)
            fila.EnJornadaActiva = fila.TieneFecha;
        return filas;
    }

    internal async Task<IReadOnlyList<SolicitudCiudadana>> ObtenerVistaCiudadanaAsync(int? id = null)
    {
        var actor = Autenticacion.Instancia.Exigir(RolUsuario.Ciudadano, RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        IReadOnlyList<SolicitudCiudadana> resultado = [];
        await db.RunInTransactionAsync(connection =>
        {
            int? propietario = Propietario(actor);
            var solicitudes = connection.Query<SolicitudRecoleccion>("""
                SELECT * FROM Solicitudes WHERE (? IS NULL OR Id = ?) AND (? IS NULL OR UsuarioId = ?)
                ORDER BY FechaRegistro DESC, Id DESC
                """, id, id, propietario, propietario);
            var equipos = connection.Query<ResiduoElectronico>("""
                SELECT r.* FROM Residuos r JOIN Solicitudes s ON s.Id = r.SolicitudId
                WHERE (? IS NULL OR s.Id = ?) AND (? IS NULL OR s.UsuarioId = ?) ORDER BY r.Id
                """, id, id, propietario, propietario).ToLookup(e => e.SolicitudId);
            var fechas = connection.Query<FechaAsignacionCiudadana>("""
                SELECT js.SolicitudId, j.FechaProgramada
                FROM JornadaSolicitudes js JOIN Jornadas j ON j.Id = js.JornadaId
                JOIN Solicitudes s ON s.Id = js.SolicitudId
                WHERE js.Activa = 1 AND j.Estado = ? AND (? IS NULL OR s.Id = ?)
                    AND (? IS NULL OR s.UsuarioId = ?)
                """, JornadaRecoleccion.Programada, id, id, propietario, propietario)
                .ToDictionary(f => f.SolicitudId, f => f.FechaProgramada);
            resultado = solicitudes.Select(s => new SolicitudCiudadana(s, equipos[s.Id].ToArray(),
                fechas.GetValueOrDefault(s.Id))).ToArray();
        });
        return resultado;
    }

    private sealed class FechaAsignacionCiudadana
    {
        public int SolicitudId { get; set; }
        public DateTime? FechaProgramada { get; set; }
    }

    internal async Task<(SolicitudRecoleccion? Solicitud, List<ResiduoElectronico> Equipos)> ObtenerCompletaAsync(int id)
    {
        var actor = Autenticacion.Instancia.Exigir(RolUsuario.Ciudadano, RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        SolicitudRecoleccion? solicitud = null;
        List<ResiduoElectronico> equipos = new();
        await db.RunInTransactionAsync(connection =>
        {
            var candidata = connection.Find<SolicitudRecoleccion>(id);
            if (candidata is not null && PuedeConsultar(actor, candidata))
            {
                solicitud = candidata;
                equipos = LeerEquipos(connection, id);
            }
        });
        return (solicitud, equipos);
    }

    internal async Task<bool> GuardarSolicitudCompletaAsync(SolicitudRecoleccion solicitud,
        IReadOnlyList<ResiduoElectronico> borrador, IReadOnlyList<ResiduoElectronico> originales,
        SolicitudRecoleccion? original, int? numeroPersonalizado = null)
    {
        var actor = Autenticacion.Instancia.Exigir(RolUsuario.Ciudadano, RolUsuario.Gestor);
        var ids = borrador.Where(e => e.Id != 0).Select(e => e.Id).ToHashSet();
        bool nueva = solicitud.Id == 0;
        if (actor.Rol == RolUsuario.Ciudadano && !nueva)
            throw new UnauthorizedAccessException("El ciudadano solo puede completar la ubicaci?n de una solicitud existente.");
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
                insertada = Insertar(connection, solicitud, numeroPersonalizado, Propietario(actor));
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

    private static int? Propietario(IdentidadUsuario actor) =>
        actor.Rol == RolUsuario.Ciudadano ? actor.Id : null;

    private static bool PuedeConsultar(IdentidadUsuario actor, SolicitudRecoleccion solicitud) =>
        actor.Rol == RolUsuario.Gestor || solicitud.UsuarioId == actor.Id;

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
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await Database.GetConnectionAsync();
        // SQLite elimina los residuos asociados dentro de la misma sentencia.
        return await db.DeleteAsync<SolicitudRecoleccion>(id) == 1;
    }

    public async Task<int> ContarAsync()
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
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
