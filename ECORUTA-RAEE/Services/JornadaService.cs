using ECORUTA_RAEE.Models;
using SQLite;

namespace ECORUTA_RAEE.Services;

internal sealed class JornadaService
{
    public static JornadaService Instancia { get; } = new();
    private JornadaService() { }

    public async Task<PanelJornadas> ObtenerPanelAsync()
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        PanelJornadas panel = null!;
        await db.RunInTransactionAsync(connection =>
        {
            var umbrales = LeerUmbrales(connection);
            var disponibles = LeerDisponibles(connection);
            var zonas = disponibles
                .Where(s => EsZonaEspecifica(s.Zona))
                .GroupBy(s => s.Zona.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(grupo => ResumirZona(grupo.Key, grupo, umbrales))
                .OrderByDescending(zona => zona.AlcanzaUmbral)
                .ThenBy(zona => zona.Zona)
                .ToList();
            var jornadas = connection.Table<JornadaRecoleccion>()
                .OrderByDescending(j => j.FechaCreacion)
                .ThenByDescending(j => j.Id)
                .ToList();
            panel = new PanelJornadas(umbrales, zonas, jornadas,
                disponibles.Count(s => !EsZonaEspecifica(s.Zona)));
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
    public async Task<JornadaRecoleccion?> ObtenerDetalleAsync(int id)
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        JornadaRecoleccion? jornada = null;
        await db.RunInTransactionAsync(connection =>
        {
            jornada = connection.Find<JornadaRecoleccion>(id);
            if (jornada is not null)
                jornada.Solicitudes = connection.Query<SolicitudRecoleccion>("""
                    SELECT s.* FROM Solicitudes s JOIN JornadaSolicitudes js ON js.SolicitudId = s.Id
                    WHERE js.JornadaId = ? ORDER BY s.FechaRegistro, s.Id
                    """, id);
        });
        return jornada;
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
            WHERE s.Estado = 'Pendiente'
              AND NOT EXISTS (SELECT 1 FROM JornadaSolicitudes js WHERE js.SolicitudId = s.Id AND js.Activa = 1)
            """;
        const string orden = " ORDER BY s.FechaRegistro, s.Id";
        return zona is null
            ? connection.Query<SolicitudRecoleccion>(consulta + orden)
            : connection.Query<SolicitudRecoleccion>(
                consulta + " AND trim(s.Zona) = ? COLLATE NOCASE" + orden, zona.Trim());
    }

    private static UmbralesJornada LeerUmbrales(SQLiteConnection connection) => new(
        connection.ExecuteScalar<int>("SELECT Cantidad FROM ConfiguracionJornadas WHERE Id = 1"),
        connection.ExecuteScalar<double>("SELECT Peso FROM ConfiguracionJornadas WHERE Id = 1"));

    private static ZonaDisponible ResumirZona(
        string zona, IEnumerable<SolicitudRecoleccion> solicitudes, UmbralesJornada umbrales) =>
        new(zona, solicitudes.Count(), solicitudes.Sum(s => s.PesoTotalEstimado), umbrales);

    private static bool EsZonaEspecifica(string zona) => !string.IsNullOrWhiteSpace(zona)
        && !zona.Trim().Equals("Otra zona", StringComparison.OrdinalIgnoreCase);
}
