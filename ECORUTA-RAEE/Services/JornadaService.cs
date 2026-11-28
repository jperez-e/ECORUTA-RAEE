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
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        PanelJornadas panel = null!;
        await db.RunInTransactionAsync(connection =>
        {
            var (umbrales, zonas, sinZona, sinUbicacion) = LeerDisponibilidad(connection);
            var jornadas = connection.Table<JornadaRecoleccion>()
                .OrderByDescending(j => j.FechaCreacion)
                .ThenByDescending(j => j.Id)
                .ToList();
            panel = new PanelJornadas(umbrales, zonas, jornadas, sinZona, sinUbicacion);
        });
        return panel;
    }

    public async Task<VistaPreviaJornada> ObtenerVistaPreviaAsync(string zona)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
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
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        if (umbrales.Cantidad < 1 || !double.IsFinite(umbrales.Peso) || umbrales.Peso <= 0)
            throw new ArgumentOutOfRangeException(nameof(umbrales));

        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        await db.ExecuteAsync("UPDATE ConfiguracionJornadas SET Cantidad = ?, Peso = ? WHERE Id = 1",
            umbrales.Cantidad, umbrales.Peso);
    }

    public async Task<JornadaRecoleccion?> CrearAsync(
        string zona, DateTime fechaProgramada, IReadOnlyCollection<int> idsConfirmados)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
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
    public async Task<JornadaRecoleccion?> ObtenerDetalleAsync(int id, bool incluirRutaCancelada = false, bool incluirUnidades = false)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        JornadaRecoleccion? jornada = null;
        await db.RunInTransactionAsync(connection =>
        {
            jornada = LeerDetalle(connection, id);
            if (jornada is not null && (jornada.Estado == JornadaRecoleccion.Programada || incluirRutaCancelada))
                LeerRuta(connection, jornada);
            if (incluirUnidades && jornada is not null)
                jornada.UnidadesEquipos = connection.ExecuteScalar<long>("""
                    SELECT COALESCE(SUM(r.Cantidad),0) FROM Residuos r
                    JOIN JornadaSolicitudes js ON js.SolicitudId = r.SolicitudId WHERE js.JornadaId = ?
                    """, id);
        });
        return jornada;
    }

    internal async Task<MapaJornada?> ObtenerMapaCampoAsync(int jornadaId, int? paradaInicial)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Recolector, RolUsuario.Gestor);
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        MapaJornada? mapa = null;
        await db.RunInTransactionAsync(c =>
        {
            if (c.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM Jornadas WHERE Id = ? AND Estado = ?)",
                jornadaId, JornadaRecoleccion.Programada) == 0 ||
                (paradaInicial is int id && c.ExecuteScalar<int>("""
                    SELECT EXISTS(SELECT 1 FROM JornadaSolicitudes WHERE JornadaId = ? AND SolicitudId = ? AND Activa = 1)
                    """, jornadaId, id) == 0))
                return;

            var jornada = LeerDetalle(c, jornadaId);
            if (jornada is null)
                return;
            LeerRuta(c, jornada);
            var vista = MapaJornada.Crear(jornada);
            mapa = jornada.Ruta is not null ? vista : vista with { Metodo = jornada.RutaDesactualizada
                ? "Orden desactualizado. Informa al gestor."
                : "El gestor aún no ha guardado un orden." };
        });
        return mapa;
    }

    private const string ColumnasParadaCampo = """
        s.Id, s.CodigoSolicitud, s.FechaRegistro, s.Zona, s.SectorOtraZona,
        s.DireccionReferencia, s.Latitud, s.Longitud, s.Estado, s.PesoTotalEstimado,
        (length(trim(COALESCE(s.TelefonoContacto, ''))) > 0) AS TieneTelefono
        """;

    internal async Task<IReadOnlyList<ResumenJornadaCampo>> ObtenerPortadaCampoAsync()
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Recolector, RolUsuario.Gestor);
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        IReadOnlyList<ResumenJornadaCampo> resultado = [];
        await db.RunInTransactionAsync(connection => resultado = LeerPortadaCampo(connection));
        return resultado;
    }

    internal static IReadOnlyList<ResumenJornadaCampo> LeerPortadaCampo(SQLiteConnection connection)
    {
        var jornadas = connection.Query<JornadaCampoFila>("""
            SELECT Id, Zona, FechaProgramada, HuellaRuta, MetodoRuta FROM Jornadas
            WHERE Estado = ? ORDER BY FechaProgramada, Id
            """, JornadaRecoleccion.Programada);
        var paradas = LeerParadasCampo(connection, null).ToLookup(p => p.JornadaId);
        return jornadas.Select(j => PrepararCampo(j, paradas[j.Id].ToArray()).Resumen).ToArray();
    }

    internal async Task<ConsultaCampo?> ObtenerConsultaCampoAsync(int jornadaId, int? solicitudId = null)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Recolector, RolUsuario.Gestor);
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        ConsultaCampo? resultado = null;
        await db.RunInTransactionAsync(connection =>
        {
            var jornada = connection.Query<JornadaCampoFila>("""
                SELECT Id, Zona, FechaProgramada, HuellaRuta, MetodoRuta FROM Jornadas
                WHERE Id = ? AND Estado = ?
                """, jornadaId, JornadaRecoleccion.Programada).SingleOrDefault();
            if (jornada is null)
                return;

            var paradas = LeerParadasCampo(connection, jornadaId);
            var consulta = PrepararCampo(jornada, paradas);
            if (solicitudId is int id)
            {
                var parada = consulta.Paradas.FirstOrDefault(p => p.Solicitud.Id == id);
                if (parada is null)
                    return;

                var contacto = connection.Query<SolicitudRecoleccion>("""
                    SELECT NombreContacto, TelefonoContacto FROM Solicitudes WHERE Id = ?
                    """, id).Single();
                parada.Solicitud.NombreContacto = contacto.NombreContacto;
                parada.Solicitud.TelefonoContacto = contacto.TelefonoContacto;
                consulta = consulta with
                {
                    Equipos = connection.Table<ResiduoElectronico>().Where(e => e.SolicitudId == id)
                        .OrderBy(e => e.Id).ToList()
                };
            }
            resultado = consulta;
        });
        return resultado;
    }

    private static List<ParadaCampoFila> LeerParadasCampo(SQLiteConnection connection, int? jornadaId)
    {
        string filtro = jornadaId.HasValue ? " AND js.JornadaId = ?" : "";
        object[] argumentos = jornadaId is int id ? [JornadaRecoleccion.Programada, id] : [JornadaRecoleccion.Programada];
        return connection.Query<ParadaCampoFila>($"""
            SELECT js.JornadaId, js.OrdenRuta, {ColumnasParadaCampo}, COALESCE(e.Unidades, 0) AS Unidades
            FROM JornadaSolicitudes js
            JOIN Jornadas j ON j.Id = js.JornadaId
            JOIN Solicitudes s ON s.Id = js.SolicitudId
            LEFT JOIN (
                SELECT r.SolicitudId, SUM(r.Cantidad) AS Unidades
                FROM Residuos r JOIN JornadaSolicitudes a ON a.SolicitudId = r.SolicitudId AND a.Activa = 1
                JOIN Jornadas aj ON aj.Id = a.JornadaId AND aj.Estado = ?
                {(jornadaId.HasValue ? "WHERE a.JornadaId = ?" : "")}
                GROUP BY r.SolicitudId
            ) e ON e.SolicitudId = s.Id
            WHERE js.Activa = 1 AND j.Estado = ?{filtro}
            ORDER BY s.FechaRegistro, s.Id
            """, [.. argumentos, .. argumentos]);
    }

    private static ConsultaCampo PrepararCampo(JornadaCampoFila jornada, IReadOnlyList<ParadaCampoFila> filas)
    {
        var ordenadas = filas.OrderBy(p => p.OrdenRuta).ToArray();
        bool vigente = filas.Count > 0 && jornada.HuellaRuta is { } huella &&
            huella == PlanificadorRutas.CalcularHuella(filas) &&
            OrdenGuardadoValido(jornada.MetodoRuta, filas.Count, ordenadas.Select(p => p.OrdenRuta));
        var estado = vigente ? EstadoOrdenCampo.Vigente : jornada.HuellaRuta is not null
            ? EstadoOrdenCampo.Desactualizado : EstadoOrdenCampo.Pendiente;
        return new(new ResumenJornadaCampo(jornada.Id, jornada.Zona, jornada.FechaProgramada, filas.Count,
                filas.Sum(p => p.Unidades), filas.Sum(p => p.PesoTotalEstimado), estado,
                filas.Count(p => p.UbicacionPendiente), filas.Count(p => !p.TieneTelefono)), vigente,
            (vigente ? ordenadas : filas).Select(p => new ParadaCampo(p, p.Unidades)).ToArray(), []);
    }

    private sealed class JornadaCampoFila
    {
        public int Id { get; set; }
        public string Zona { get; set; } = "";
        public DateTime FechaProgramada { get; set; }
        public string? HuellaRuta { get; set; }
        public int? MetodoRuta { get; set; }
    }

    private sealed class ParadaCampoFila : SolicitudRecoleccion
    {
        public int JornadaId { get; set; }
        public int? OrdenRuta { get; set; }
        public long Unidades { get; set; }
        public bool TieneTelefono { get; set; }
    }

    public async Task<JornadaRecoleccion?> GenerarRutaAsync(int id)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
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

        if (!OrdenGuardadoValido((int)metodo, jornada.Solicitudes.Count, guardadas.Select(p => (int?)p.Orden)))
        {
            jornada.RutaDesactualizada = true;
            return;
        }

        var solicitudes = jornada.Solicitudes.ToDictionary(s => s.Id);
        jornada.Ruta = new PlanificacionRuta(metodo, guardadas
            .Select(p => new ParadaRuta(solicitudes[p.SolicitudId], p.Orden, p.DistanciaKm))
            .ToList());
    }

    private static bool OrdenGuardadoValido(int? metodo, int cantidad, IEnumerable<int?> orden) =>
        metodo is (int)MetodoRuta.VecinoMasCercano or (int)MetodoRuta.Antiguedad &&
        orden.SequenceEqual(Enumerable.Range(1, cantidad).Select(i => (int?)i));

    private sealed class OrdenRutaGuardado
    {
        public int SolicitudId { get; set; }
        public int Orden { get; set; }
        public double? DistanciaKm { get; set; }
    }

    public async Task<bool> CancelarAsync(int id)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        return await db.ExecuteAsync("UPDATE Jornadas SET Estado = ? WHERE Id = ? AND Estado = ?",
            JornadaRecoleccion.Cancelada, id, JornadaRecoleccion.Programada) == 1;
    }

    public async Task<bool> TieneJornadasAsync(int solicitudId)
    {
        Autenticacion.Instancia.Exigir(RolUsuario.Gestor);
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        return await db.ExecuteScalarAsync<int>(
            "SELECT EXISTS(SELECT 1 FROM JornadaSolicitudes WHERE SolicitudId = ?)", solicitudId) == 1;
    }

    private static List<SolicitudRecoleccion> LeerDisponibles(SQLiteConnection connection, string zona) =>
        LeerPendientesSinAsignar(connection).Where(s => ElegibilidadRecogida.EsElegible(s) &&
            string.Equals(s.Zona.Trim(), zona.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    private static List<SolicitudRecoleccion> LeerPendientesSinAsignar(SQLiteConnection connection)
    {
        const string consulta = """
            SELECT s.* FROM Solicitudes s
            WHERE
            """ + " " + FiltroDisponible;
        return connection.Query<SolicitudRecoleccion>(consulta + OrdenDisponibilidad);
    }

    internal static (UmbralesJornada Umbrales, IReadOnlyList<ZonaDisponible> Zonas, int SinZona, int SinUbicacion)
        LeerDisponibilidad(SQLiteConnection connection)
    {
        var umbrales = LeerUmbrales(connection);
        var pendientes = connection.Query<SolicitudRecoleccion>("""
            SELECT s.Zona, s.Estado, s.PesoTotalEstimado, s.Latitud, s.Longitud
            FROM Solicitudes s WHERE
            """ + " " + FiltroDisponible);
        var elegibles = pendientes.Where(ElegibilidadRecogida.EsElegible)
            .Select(s => new TotalesSolicitudesZona { Zona = s.Zona, Cantidad = 1, Peso = s.PesoTotalEstimado });
        var zonas = AgruparZonas(elegibles)
            .Select(z => new ZonaDisponible(z.Zona, z.Cantidad, z.Peso, umbrales))
            .OrderByDescending(z => z.AlcanzaUmbral)
            .ThenBy(z => z.Zona)
            .ToList();
        return (umbrales, zonas, pendientes.Count(s => !EsZonaEspecifica(s.Zona)),
            connection.Query<SolicitudRecoleccion>(
                "SELECT Latitud, Longitud FROM Solicitudes WHERE Estado = ?", ElegibilidadRecogida.Pendiente)
                .Count(s => s.UbicacionPendiente));
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

    internal static bool EsZonaEspecifica(string zona) => ElegibilidadRecogida.ZonaAdmitida(zona);
}
