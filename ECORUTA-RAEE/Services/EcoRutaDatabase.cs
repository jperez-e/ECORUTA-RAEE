using Microsoft.Maui.Storage;
using SQLite;

namespace ECORUTA_RAEE.Services;

internal sealed class EcoRutaDatabase
{
    private const int SchemaVersion = 3;
    private const string FileName = "ecoruta-raee.db3";
    private static readonly Lazy<EcoRutaDatabase> DefaultInstance =
        new(() => new EcoRutaDatabase(Path.Combine(FileSystem.AppDataDirectory, FileName)));

    private readonly string _databasePath;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public static EcoRutaDatabase Instance => DefaultInstance.Value;

    internal EcoRutaDatabase(string databasePath)
    {
        _databasePath = databasePath;
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        await _initializationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is not null)
                return _connection;

            var options = new SQLiteConnectionString(
                _databasePath,
                storeDateTimeAsTicks: true,
                postKeyAction: EnableForeignKeys);
            var connection = new SQLiteAsyncConnection(options);

            await connection.RunInTransactionAsync(ApplyMigrations)
                .ConfigureAwait(false);
            _connection = connection;
            return _connection;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private static void EnableForeignKeys(SQLiteConnection connection)
    {
        // sqlite-net llama a esta acción al abrir cada conexión física.
        connection.Execute("PRAGMA foreign_keys = ON");
        if (connection.ExecuteScalar<int>("PRAGMA foreign_keys") != 1)
            throw new InvalidOperationException("SQLite no pudo habilitar las claves foráneas.");
    }

    private static void ApplyMigrations(SQLiteConnection connection)
    {
        int version = connection.ExecuteScalar<int>("PRAGMA user_version");
        if (version > SchemaVersion)
            throw new InvalidOperationException("La base de datos pertenece a una versión más reciente de EcoRuta-RAEE.");

        if (version == 0)
            CreateInitialSchema(connection);
        if (version < 2)
            CrearEsquemaJornadas(connection);
        if (version < 3)
            CrearEsquemaRutas(connection);
    }

    private static void CreateInitialSchema(SQLiteConnection connection)
    {
        connection.Execute("""
            CREATE TABLE Solicitudes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CodigoSolicitud TEXT NOT NULL COLLATE NOCASE CHECK (length(trim(CodigoSolicitud)) > 0),
                FechaRegistro INTEGER NOT NULL,
                Zona TEXT NOT NULL,
                DireccionReferencia TEXT NOT NULL,
                Latitud REAL,
                Longitud REAL,
                Estado TEXT NOT NULL,
                PesoTotalEstimado REAL NOT NULL
            )
            """);
        connection.Execute("""
            CREATE UNIQUE INDEX UX_Solicitudes_CodigoSolicitud ON Solicitudes (CodigoSolicitud)
            """);

        connection.Execute("""
            CREATE TABLE Residuos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SolicitudId INTEGER NOT NULL REFERENCES Solicitudes (Id) ON DELETE CASCADE,
                Categoria TEXT NOT NULL,
                Descripcion TEXT NOT NULL,
                Cantidad INTEGER NOT NULL,
                PesoEstimado REAL NOT NULL
            )
            """);
        connection.Execute("CREATE INDEX IX_Residuos_SolicitudId ON Residuos (SolicitudId)");

        // En la etapa 2 se reservará el número y se insertará la solicitud
        // en una sola transacción. El valor no se deriva de las solicitudes existentes.
        connection.Execute("""
            CREATE TABLE SecuenciaCodigoSolicitud (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                UltimoNumero INTEGER NOT NULL CHECK (UltimoNumero >= 0)
            )
            """);
        connection.Execute("INSERT INTO SecuenciaCodigoSolicitud (Id, UltimoNumero) VALUES (1, 0)");
        connection.Execute("PRAGMA user_version = 1");
    }

    private static void CrearEsquemaJornadas(SQLiteConnection connection)
    {
        connection.Execute("""
            CREATE TABLE Jornadas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Zona TEXT NOT NULL COLLATE NOCASE
                    CHECK (length(trim(Zona)) > 0 AND trim(Zona) <> 'Otra zona' COLLATE NOCASE),
                FechaCreacion INTEGER NOT NULL,
                FechaProgramada INTEGER NOT NULL,
                Estado TEXT NOT NULL CHECK (Estado IN ('Programada', 'Cancelada'))
            )
            """);
        connection.Execute("""
            CREATE TABLE JornadaSolicitudes (
                JornadaId INTEGER NOT NULL REFERENCES Jornadas(Id) ON DELETE CASCADE,
                SolicitudId INTEGER NOT NULL REFERENCES Solicitudes(Id) ON DELETE CASCADE,
                Activa INTEGER NOT NULL DEFAULT 1 CHECK (Activa IN (0, 1)),
                PRIMARY KEY (JornadaId, SolicitudId)
            )
            """);
        connection.Execute("""
            CREATE UNIQUE INDEX UX_JornadaSolicitudes_Activa ON JornadaSolicitudes(SolicitudId)
            WHERE Activa = 1
            """);
        connection.Execute("CREATE INDEX IX_JornadaSolicitudes_SolicitudId ON JornadaSolicitudes(SolicitudId)");
        connection.Execute("""
            CREATE TABLE ConfiguracionJornadas (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Cantidad INTEGER NOT NULL CHECK (Cantidad > 0),
                Peso REAL NOT NULL CHECK (Peso > 0)
            )
            """);
        connection.Execute("INSERT INTO ConfiguracionJornadas(Id, Cantidad, Peso) VALUES (1, 3, 50)");

        connection.Execute("""
            CREATE TRIGGER ValidarAsignacionJornada BEFORE INSERT ON JornadaSolicitudes
            WHEN NEW.Activa <> 1 OR NOT EXISTS (
                SELECT 1 FROM Jornadas j JOIN Solicitudes s ON s.Id = NEW.SolicitudId
                WHERE j.Id = NEW.JornadaId AND j.Estado = 'Programada' AND s.Estado = 'Pendiente'
                AND trim(s.Zona) = j.Zona COLLATE NOCASE
            )
            BEGIN
                SELECT RAISE(ABORT, 'La solicitud ya no cumple los criterios de la jornada.');
            END
            """);
        connection.Execute("""
            CREATE TRIGGER ProtegerAsignacionJornada
            BEFORE UPDATE OF JornadaId, SolicitudId, Activa ON JornadaSolicitudes
            WHEN NEW.JornadaId <> OLD.JornadaId OR NEW.SolicitudId <> OLD.SolicitudId
              OR (NEW.Activa <> OLD.Activa AND (
                  NEW.Activa <> 0 OR NOT EXISTS (
                      SELECT 1 FROM Jornadas WHERE Id = OLD.JornadaId AND Estado = 'Cancelada'
                  )
              ))
            BEGIN
                SELECT RAISE(ABORT, 'La asignación de la jornada no se puede modificar.');
            END
            """);
        connection.Execute("""
            CREATE TRIGGER ProtegerZonaSolicitudAsignada BEFORE UPDATE OF Zona ON Solicitudes
            WHEN trim(NEW.Zona) <> trim(OLD.Zona) COLLATE NOCASE
              AND EXISTS (SELECT 1 FROM JornadaSolicitudes WHERE SolicitudId = OLD.Id AND Activa = 1)
            BEGIN
                SELECT RAISE(ABORT, 'La solicitud está asignada a una jornada activa.');
            END
            """);
        connection.Execute("""
            CREATE TRIGGER ProtegerEstadoJornada BEFORE UPDATE OF Estado, Zona ON Jornadas
            WHEN NEW.Zona <> OLD.Zona COLLATE NOCASE
              OR (OLD.Estado = 'Cancelada' AND NEW.Estado <> OLD.Estado)
            BEGIN
                SELECT RAISE(ABORT, 'No se puede cambiar la zona ni reactivar una jornada cancelada.');
            END
            """);
        connection.Execute("""
            CREATE TRIGGER LiberarSolicitudesJornada AFTER UPDATE OF Estado ON Jornadas
            WHEN OLD.Estado = 'Programada' AND NEW.Estado = 'Cancelada'
            BEGIN
                UPDATE JornadaSolicitudes SET Activa = 0 WHERE JornadaId = NEW.Id AND Activa = 1;
            END
            """);
        connection.Execute("""
            CREATE TRIGGER CancelarJornadaVacia AFTER DELETE ON JornadaSolicitudes
            WHEN OLD.Activa = 1 AND NOT EXISTS (
                SELECT 1 FROM JornadaSolicitudes WHERE JornadaId = OLD.JornadaId AND Activa = 1
            )
            BEGIN
                UPDATE Jornadas SET Estado = 'Cancelada' WHERE Id = OLD.JornadaId AND Estado = 'Programada';
            END
            """);
        connection.Execute("PRAGMA user_version = 2");
    }

    private static void CrearEsquemaRutas(SQLiteConnection connection)
    {
        connection.Execute("ALTER TABLE Jornadas ADD COLUMN MetodoRuta INTEGER CHECK (MetodoRuta IN (1, 2))");
        connection.Execute("ALTER TABLE Jornadas ADD COLUMN HuellaRuta TEXT");
        connection.Execute("ALTER TABLE JornadaSolicitudes ADD COLUMN OrdenRuta INTEGER CHECK (OrdenRuta > 0)");
        connection.Execute("ALTER TABLE JornadaSolicitudes ADD COLUMN DistanciaRutaKm REAL CHECK (DistanciaRutaKm >= 0)");
        connection.Execute("""
            CREATE UNIQUE INDEX UX_JornadaSolicitudes_OrdenRuta
            ON JornadaSolicitudes(JornadaId, OrdenRuta) WHERE OrdenRuta IS NOT NULL
            """);
        connection.Execute("PRAGMA user_version = 3");
    }
}
