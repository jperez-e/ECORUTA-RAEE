using ECORUTA_RAEE.Models;
using SQLite;

namespace ECORUTA_RAEE.Services;

internal sealed class JornadaService
{
    public static JornadaService Instancia { get; } = new();
    private JornadaService() { }

    private const string FiltroDisponible = """
        s.Estado = 'Pendiente'
        AND NOT EXISTS (SELECT 1 FROM JornadaSolicitudes js WHERE js.SolicitudId = s.Id AND js.Activa = 1)
        """;
    private const string OrdenDisponibilidad = " ORDER BY s.FechaRegistro, s.Id";

    public async Task<PanelJornadas> ObtenerPanelAsync()
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        PanelJornadas panel = null!;
        await db.RunInTransactionAsync(connection =>
        {
            var (umbrales, zonas, sinZona) = LeerDisponibilidad(connection);
            var jornadas = connection.Table<JornadaRecoleccion>()
                .OrderByDescending(j => j.FechaCreacion)
                .ThenByDescending(j => j.Id)
                .ToList();
            panel = new PanelJornadas(umbrales, zonas, jornadas, sinZona);
        });
        return panel;
    }

    public async Task<VistaPreviaJornada> ObtenerVistaPreviaAsync(string zona)
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        VistaPreviaJornada vista = null!;
        await db.RunInTransactionAsync(connection =>
        {
            var solicitudes = EsZonaEspecifica(zona)
                ? LeerDisponibles(connection, zona)
                : new List<SolicitudRecoleccion>();
            vista = new VistaPreviaJornada(
                ResumirZona(zona.Trim(), solicitudes, LeerUmbrales(connection)), solicitudes);
        });
        return vista;
    }

    public async Task GuardarUmbralesAsync(UmbralesJornada umbrales)
    {
        if (umbrales.Cantidad < 1 || !double.IsFinite(umbrales.Peso) || umbrales.Peso <= 0)
            throw new ArgumentOutOfRangeException(nameof(umbrales));

        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        await db.ExecuteAsync("UPDATE ConfiguracionJornadas SET Cantidad = ?, Peso = ? WHERE Id = 1",
            umbrales.Cantidad, umbrales.Peso);
    }

    public async Task<JornadaRecoleccion?> CrearAsync(
        string zona, DateTime fechaProgramada, IReadOnlyCollection<int> idsConfirmados)
    {
        if (!EsZonaEspecifica(zona) || fechaProgramada.Date < DateTime.Today)
            return null;

        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        JornadaRecoleccion? creada = null;
        await db.RunInTransactionAsync(connection =>
        {
            string zonaNormalizada = zona.Trim();
            var solicitudes = LeerDisponibles(connection, zonaNormalizada);
            var umbrales = LeerUmbrales(connection);
            if (!ResumirZona(zonaNormalizada, solicitudes, umbrales).AlcanzaUmbral
                || idsConfirmados.Count != solicitudes.Count
                || !idsConfirmados.ToHashSet().SetEquals(solicitudes.Select(s => s.Id)))
                return;

            var jornada = new JornadaRecoleccion
            {
                Zona = zonaNormalizada,
                FechaCreacion = DateTime.Now,
                FechaProgramada = fechaProgramada.Date,
                Solicitudes = solicitudes
            };
            connection.Insert(jornada);
            foreach (var solicitud in solicitudes)
                connection.Execute("INSERT INTO JornadaSolicitudes (JornadaId, SolicitudId) VALUES (?, ?)",
                    jornada.Id, solicitud.Id);

            creada = jornada;
        });
        return creada;
    }

    // Incluye los modelos completos, con las coordenadas opcionales de las solicitudes.
    public async Task<JornadaRecoleccion?> ObtenerDetalleAsync(int id, bool incluirRutaCancelada = false)
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        JornadaRecoleccion? jornada = null;
        await db.RunInTransactionAsync(connection =>
        {
            jornada = LeerDetalle(connection, id);
            if (jornada is not null && (jornada.Estado == JornadaRecoleccion.Programada || incluirRutaCancelada))
                LeerRuta(connection, jornada);
        });
        return jornada;
    }

    internal async Task<IReadOnlyList<JornadaRecoleccion>> ObtenerProgramadasAsync()
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        var jornadas = new List<JornadaRecoleccion>();
        await db.RunInTransactionAsync(connection =>
        {
            jornadas = connection.Table<JornadaRecoleccion>()
                .Where(j => j.Estado == JornadaRecoleccion.Programada)
                .OrderBy(j => j.FechaProgramada).ThenBy(j => j.Id).ToList();
            var asignaciones = connection.Query<AsignacionProgramada>("""
                SELECT js.JornadaId, s.* FROM JornadaSolicitudes js
                JOIN Jornadas j ON j.Id = js.JornadaId
                JOIN Solicitudes s ON s.Id = js.SolicitudId
                WHERE j.Estado = ? AND js.Activa = 1
                """, JornadaRecoleccion.Programada).ToLookup(s => s.JornadaId);
            foreach (var jornada in jornadas)
                jornada.Solicitudes = asignaciones[jornada.Id].ToArray();
        });
        return jornadas;
    }

    private sealed class AsignacionProgramada : SolicitudRecoleccion
    {
        public int JornadaId { get; set; }
    }

    public async Task<JornadaRecoleccion?> GenerarRutaAsync(int id)
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        JornadaRecoleccion? actualizada = null;
        await db.RunInTransactionAsync(connection =>
        {
            var jornada = LeerDetalle(connection, id);
            if (jornada is not { Estado: JornadaRecoleccion.Programada } || jornada.Solicitudes.Count == 0)
                return;

            var ruta = PlanificadorRutas.Calcular(jornada.Solicitudes);
            connection.Execute("""
                UPDATE JornadaSolicitudes SET OrdenRuta = NULL, DistanciaRutaKm = NULL WHERE JornadaId = ?
                """, id);
            foreach (var parada in ruta.Paradas)
                connection.Execute("""
                    UPDATE JornadaSolicitudes SET OrdenRuta = ?, DistanciaRutaKm = ?
                    WHERE JornadaId = ? AND SolicitudId = ?
                    """, parada.Orden, parada.DistanciaKm, id, parada.Solicitud.Id);

            connection.Execute("UPDATE Jornadas SET MetodoRuta = ?, HuellaRuta = ? WHERE Id = ?",
                (int)ruta.Metodo, PlanificadorRutas.CalcularHuella(jornada.Solicitudes), id);
            jornada.Ruta = ruta;
            actualizada = jornada;
        });
        return actualizada;
    }

    private static JornadaRecoleccion? LeerDetalle(SQLiteConnection connection, int id)
    {
        var jornada = connection.Find<JornadaRecoleccion>(id);
        if (jornada is not null)
            jornada.Solicitudes = connection.Query<SolicitudRecoleccion>("""
                SELECT s.* FROM Solicitudes s JOIN JornadaSolicitudes js ON js.SolicitudId = s.Id
                WHERE js.JornadaId = ? ORDER BY s.FechaRegistro, s.Id
                """, id);
        return jornada;
    }

    private static void LeerRuta(SQLiteConnection connection, JornadaRecoleccion jornada)
    {
        string? huella = connection.ExecuteScalar<string>("SELECT HuellaRuta FROM Jornadas WHERE Id = ?", jornada.Id);
        if (huella is null)
            return;

        if (huella != PlanificadorRutas.CalcularHuella(jornada.Solicitudes))
        {
            jornada.RutaDesactualizada = true;
            return;
        }

        var metodo = (MetodoRuta)connection.ExecuteScalar<int>("SELECT MetodoRuta FROM Jornadas WHERE Id = ?", jornada.Id);
        var guardadas = connection.Query<OrdenRutaGuardado>("""
            SELECT SolicitudId, OrdenRuta AS Orden, DistanciaRutaKm AS DistanciaKm
            FROM JornadaSolicitudes WHERE JornadaId = ? AND OrdenRuta IS NOT NULL ORDER BY OrdenRuta
            """, jornada.Id);

        if (guardadas.Count != jornada.Solicitudes.Count ||
            !guardadas.Select(p => p.Orden).SequenceEqual(Enumerable.Range(1, guardadas.Count)) ||
            metodo is not (MetodoRuta.VecinoMasCercano or MetodoRuta.Antiguedad))
        {
            jornada.RutaDesactualizada = true;
            return;
        }

        var solicitudes = jornada.Solicitudes.ToDictionary(s => s.Id);
        jornada.Ruta = new PlanificacionRuta(metodo, guardadas
            .Select(p => new ParadaRuta(solicitudes[p.SolicitudId], p.Orden, p.DistanciaKm))
            .ToList());
    }

    private sealed class OrdenRutaGuardado
    {
        public int SolicitudId { get; set; }
        public int Orden { get; set; }
        public double? DistanciaKm { get; set; }
    }

    public async Task<bool> CancelarAsync(int id)
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        return await db.ExecuteAsync("UPDATE Jornadas SET Estado = ? WHERE Id = ? AND Estado = ?",
            JornadaRecoleccion.Cancelada, id, JornadaRecoleccion.Programada) == 1;
    }

    public async Task<bool> TieneJornadasAsync(int solicitudId)
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        return await db.ExecuteScalarAsync<int>(
            "SELECT EXISTS(SELECT 1 FROM JornadaSolicitudes WHERE SolicitudId = ?)", solicitudId) == 1;
    }

    private static List<SolicitudRecoleccion> LeerDisponibles(SQLiteConnection connection, string? zona = null)
    {
        const string consulta = """
            SELECT s.* FROM Solicitudes s
            WHERE
            """ + " " + FiltroDisponible;
        return zona is null
            ? connection.Query<SolicitudRecoleccion>(consulta + OrdenDisponibilidad)
            : connection.Query<SolicitudRecoleccion>(
                consulta + " AND trim(s.Zona) = ? COLLATE NOCASE" + OrdenDisponibilidad, zona.Trim());
    }

    internal static (UmbralesJornada Umbrales, IReadOnlyList<ZonaDisponible> Zonas, int SinZona)
        LeerDisponibilidad(SQLiteConnection connection)
    {
        var umbrales = LeerUmbrales(connection);
        var disponibles = connection.Query<TotalesSolicitudesZona>($"""
            SELECT s.Zona, 1 AS Cantidad, s.PesoTotalEstimado AS Peso
            FROM Solicitudes s WHERE {FiltroDisponible}{OrdenDisponibilidad}
            """);
        var zonas = AgruparZonas(disponibles.Where(z => EsZonaEspecifica(z.Zona)))
            .Select(z => new ZonaDisponible(z.Zona, z.Cantidad, z.Peso, umbrales))
            .OrderByDescending(z => z.AlcanzaUmbral)
            .ThenBy(z => z.Zona)
            .ToList();
        return (umbrales, zonas, disponibles.Where(z => !EsZonaEspecifica(z.Zona)).Sum(z => z.Cantidad));
    }

    internal static IEnumerable<TotalesSolicitudesZona> AgruparZonas(IEnumerable<TotalesSolicitudesZona> zonas) =>
        zonas.GroupBy(z => z.Zona.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grupo => new TotalesSolicitudesZona
            {
                Zona = grupo.Key,
                Cantidad = grupo.Sum(z => z.Cantidad),
                Pendientes = grupo.Sum(z => z.Pendientes),
                Peso = grupo.Sum(z => z.Peso)
            });

    private static UmbralesJornada LeerUmbrales(SQLiteConnection connection) => new(
        connection.ExecuteScalar<int>("SELECT Cantidad FROM ConfiguracionJornadas WHERE Id = 1"),
        connection.ExecuteScalar<double>("SELECT Peso FROM ConfiguracionJornadas WHERE Id = 1"));

    private static ZonaDisponible ResumirZona(
        string zona, IEnumerable<SolicitudRecoleccion> solicitudes, UmbralesJornada umbrales) =>
        new(zona, solicitudes.Count(), solicitudes.Sum(s => s.PesoTotalEstimado), umbrales);

    internal static bool EsZonaEspecifica(string zona) => !string.IsNullOrWhiteSpace(zona)
        && !zona.Trim().Equals("Otra zona", StringComparison.OrdinalIgnoreCase);
}
