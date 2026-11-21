using Microsoft.Maui.Storage;
using SQLite;

namespace ECORUTA_RAEE.Services;

internal sealed class EcoRutaDatabase
{
    private const int SchemaVersion = 1;
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
}
