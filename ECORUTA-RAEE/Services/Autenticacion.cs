using System.Net.Mail;
using System.Security.Cryptography;
using ECORUTA_RAEE.Models;
using SQLite;

namespace ECORUTA_RAEE.Services;

internal sealed class Autenticacion(EcoRutaDatabase database, TimeProvider reloj)
{
    internal static Autenticacion Instancia { get; } = new(EcoRutaDatabase.Instance, TimeProvider.System);
    internal const int Iteraciones = 600_000;
    private const int Version = 1;
    private static readonly TimeSpan Vigencia = TimeSpan.FromDays(14);
    private readonly SemaphoreSlim _acceso = new(1);
    private IdentidadUsuario? _actual;
    private byte[]? _huella;
    private DateTime? _vence;
    private int _fallos;
    private DateTime _pausa;
    internal IdentidadUsuario? Actual => _vence is { } vence && vence <= Ahora ? null : _actual;
    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    private static byte[] Derivar(string contrasena, byte[] salt, int iteraciones)
    {
#if ANDROID && DEBUG
        long inicio = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
        byte[] resultado = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, iteraciones, HashAlgorithmName.SHA256, 32);
#if ANDROID && DEBUG
        Android.Util.Log.Debug("EcoRuta.Derivacion", FormattableString.Invariant(
            $"PBKDF2 {iteraciones} iteraciones · {System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds:F0} ms"));
#endif
        return resultado;
    }

    internal static Dictionary<string, string> Validar(DatosCuenta datos, string? clave, string? confirmar)
    {
        var errores = new Dictionary<string, string>();
        if (datos.Nombre.Trim().Length is < 2 or > 120 || datos.Nombre.Any(char.IsControl))
            errores["nombre"] = "Introduce un nombre de 2 a 120 caracteres.";
        string correo = NormalizarCorreo(datos.Correo);
        if (correo.Length > 254 || correo.Any(char.IsWhiteSpace) ||
            !MailAddress.TryCreate(correo, out var parsed) || parsed.Address != correo || !correo.Contains('@'))
            errores["correo"] = "Introduce un correo electrónico válido.";
        if (!DatosContacto.TryNormalizarTelefono(datos.Telefono, false, out _))
            errores["telefono"] = "Introduce un teléfono con prefijo internacional, por ejemplo +1.";
        if (clave is not null)
        {
            if (clave.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(clave))
                errores["clave"] = "Usa entre 12 y 128 caracteres para la contraseña.";
            if (clave != confirmar)
                errores["confirmar"] = "Las contraseñas no coinciden.";
        }
        return errores;
    }

    private static string NormalizarCorreo(string correo) => correo.Trim().Normalize().ToLowerInvariant();

    internal async Task<bool> RequiereGestorAsync() =>
        await (await database.GetConnectionAsync()).ExecuteScalarAsync<int>(
            "SELECT EXISTS(SELECT 1 FROM Usuarios WHERE Rol = 'Gestor')") == 0;

    internal Task AprovisionarAsync(DatosCuenta datos, string clave) => CrearAsync(datos, clave, RolUsuario.Gestor);
    internal Task RegistrarAsync(DatosCuenta datos, string clave) => CrearAsync(datos, clave, RolUsuario.Ciudadano);
    internal Task CrearRecolectorAsync(DatosCuenta datos, string clave)
    {
        Exigir(RolUsuario.Gestor);
        return CrearAsync(datos, clave, RolUsuario.Recolector);
    }

    private async Task CrearAsync(DatosCuenta datos, string clave, RolUsuario rol)
    {
        if (Validar(datos, clave, clave).Count != 0)
            throw new ArgumentException("Comprueba los datos de la cuenta.");
        var salt = RandomNumberGenerator.GetBytes(24);
        var hash = await Task.Run(() => Derivar(clave, salt, Iteraciones));
        var db = await database.GetConnectionAsync();
        try
        {
            await db.RunInTransactionAsync(c =>
            {
                if (rol == RolUsuario.Recolector)
                    Exigir(RolUsuario.Gestor);
                if (rol != RolUsuario.Recolector && Actual is not null)
                    throw new UnauthorizedAccessException();
                bool existeGestor = c.ExecuteScalar<int>("SELECT EXISTS(SELECT 1 FROM Usuarios WHERE Rol = 'Gestor')") != 0;
                if ((rol == RolUsuario.Gestor) == existeGestor)
                    throw new InvalidOperationException("La configuración inicial ya cambió. Vuelve al inicio de sesión.");
                DatosContacto.TryNormalizarTelefono(datos.Telefono, false, out var telefono);
                c.Insert(new Usuario
                {
                    Nombre = datos.Nombre.Trim(), Correo = NormalizarCorreo(datos.Correo), Telefono = telefono,
                    Hash = hash, Salt = salt, Iteraciones = Iteraciones, VersionDerivacion = Version,
                    Rol = rol.ToString(), Activo = true, FechaCreacion = Ahora
                });
            });
        }
        catch (SQLiteException ex) when (CorreoDuplicado(ex)) { throw new CorreoDuplicadoException(); }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    private static bool CorreoDuplicado(SQLiteException ex) =>
        ((int)ex.Result & 255) == (int)SQLite3.Result.Constraint &&
        ex.Message.Contains("Usuarios.Correo", StringComparison.OrdinalIgnoreCase);

    internal async Task<(IdentidadUsuario? Identidad, string? Token)> EntrarAsync(string correo, string clave, bool recordar)
    {
        await _acceso.WaitAsync();
        try
        {
            if (Actual is not null || Ahora < _pausa || correo.Length > 254 || clave.Length > 128)
                return (null, null);

            var db = await database.GetConnectionAsync();
            var fila = (await db.QueryAsync<Usuario>("SELECT * FROM Usuarios WHERE Correo = ?", NormalizarCorreo(correo))).SingleOrDefault();
            bool candidata = fila is { Activo: true, VersionDerivacion: Version, Iteraciones: >= Iteraciones and <= 5_000_000 };
            byte[] derivada = await Task.Run(() => Derivar(clave, candidata ? fila!.Salt : new byte[24],
                candidata ? fila!.Iteraciones : Iteraciones));
            bool valida = CryptographicOperations.FixedTimeEquals(derivada, candidata ? fila!.Hash : new byte[32]);
            CryptographicOperations.ZeroMemory(derivada);
            if (!valida || !candidata)
            {
                if (++_fallos >= 5)
                {
                    _pausa = Ahora.AddSeconds(30);
                    _fallos = 0;
                }
                return (null, null);
            }

            var identidad = Identificar(fila!);
            string? token = recordar ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : null;
            byte[]? huella = Huella(token);
            DateTime? vence = recordar ? Ahora.Add(Vigencia) : null;
            await db.RunInTransactionAsync(c =>
            {
                var vigente = c.Find<Usuario>(identidad.Id);
                if (vigente is not { Activo: true } || !CryptographicOperations.FixedTimeEquals(vigente.Hash, fila!.Hash))
                    throw new UnauthorizedAccessException();
                if (huella is not null)
                {
                    c.Execute("DELETE FROM Sesiones WHERE VenceUtc <= ?", Ahora.Ticks);
                    c.Execute("INSERT INTO Sesiones(Huella, UsuarioId, VenceUtc) VALUES (?, ?, ?)",
                        huella, identidad.Id, vence!.Value.Ticks);
                }
            });
            _fallos = 0;
            _huella = huella;
            _vence = vence;
            _actual = identidad;
            return (identidad, token);
        }
        finally { _acceso.Release(); }
    }

    private static byte[]? Huella(string? token)
    {
        if (token is not { Length: 44 })
            return null;
        Span<byte> bytes = stackalloc byte[32];
        if (!Convert.TryFromBase64String(token, bytes, out int longitud) || longitud != 32)
            return null;
        var huella = SHA256.HashData(bytes);
        CryptographicOperations.ZeroMemory(bytes);
        return huella;
    }

    private sealed class SesionRestaurada
    {
        public int UsuarioId { get; set; }
        public long VenceUtc { get; set; }
    }

    internal async Task<IdentidadUsuario?> RestaurarAsync(string? token)
    {
        var huella = Huella(token);
        if (huella is null)
            return null;
        await _acceso.WaitAsync();
        try
        {
            if (Actual is not null)
                return null;
            var db = await database.GetConnectionAsync();
            Usuario? cuenta = null;
            SesionRestaurada? sesion = null;
            await db.RunInTransactionAsync(c =>
            {
                sesion = c.Query<SesionRestaurada>("""
                    SELECT UsuarioId, VenceUtc FROM Sesiones
                    WHERE Huella = ? AND VenceUtc > ? AND RevocadaUtc IS NULL
                    """, huella, Ahora.Ticks).SingleOrDefault();
                if (sesion is not null)
                    cuenta = c.Find<Usuario>(sesion.UsuarioId);
            });
            if (cuenta is not { Activo: true } || sesion is null)
                return null;
            _huella = huella;
            _vence = new DateTime(sesion.VenceUtc, DateTimeKind.Utc);
            return _actual = Identificar(cuenta);
        }
        finally { _acceso.Release(); }
    }

    internal async Task SalirAsync()
    {
        await _acceso.WaitAsync();
        try
        {
            var huella = _huella;
            var db = huella is null ? null : await database.GetConnectionAsync();
            if (db is not null)
                await db.ExecuteAsync("UPDATE Sesiones SET RevocadaUtc = ? WHERE Huella = ?", Ahora.Ticks, huella);
            _actual = null;
            _huella = null;
            _vence = null;
        }
        finally { _acceso.Release(); }
    }

    internal IdentidadUsuario Exigir(RolUsuario rol, RolUsuario? alternativa = null)
    {
        if (Actual is not { Activo: true } actor || (actor.Rol != rol && actor.Rol != alternativa))
            throw new UnauthorizedAccessException("Esta cuenta no tiene permiso para realizar la operación.");
        return actor;
    }

    internal async Task<IReadOnlyList<IdentidadUsuario>> RecolectoresAsync()
    {
        Exigir(RolUsuario.Gestor);
        var db = await database.GetConnectionAsync();
        var filas = await db.QueryAsync<Usuario>("""
            SELECT Id, Nombre, Correo, Telefono, Rol, Activo FROM Usuarios
            WHERE Rol = 'Recolector' ORDER BY Nombre, Id
            """);
        return filas.Select(Identificar).ToArray();
    }

    internal async Task ActualizarRecolectorAsync(int id, DatosCuenta datos)
    {
        Exigir(RolUsuario.Gestor);
        if (Validar(datos, null, null).Count != 0)
            throw new ArgumentException("Comprueba los datos de la cuenta.");
        DatosContacto.TryNormalizarTelefono(datos.Telefono, false, out var telefono);
        var db = await database.GetConnectionAsync();
        try
        {
            await db.RunInTransactionAsync(c =>
            {
                Exigir(RolUsuario.Gestor);
                ComprobarRecolector(c.Execute("""
                    UPDATE Usuarios SET Nombre = ?, Correo = ?, Telefono = ? WHERE Id = ? AND Rol = 'Recolector'
                    """, datos.Nombre.Trim(), NormalizarCorreo(datos.Correo), telefono, id));
            });
        }
        catch (SQLiteException ex) when (CorreoDuplicado(ex)) { throw new CorreoDuplicadoException(); }
    }

    internal async Task EstadoRecolectorAsync(int id, bool activo)
    {
        Exigir(RolUsuario.Gestor);
        var db = await database.GetConnectionAsync();
        await db.RunInTransactionAsync(c =>
        {
            Exigir(RolUsuario.Gestor);
            ComprobarRecolector(c.Execute("UPDATE Usuarios SET Activo = ? WHERE Id = ? AND Rol = 'Recolector'", activo, id));
            if (!activo)
                Revocar(c, id);
        });
    }

    internal async Task RestablecerRecolectorAsync(int id, string clave)
    {
        Exigir(RolUsuario.Gestor);
        if (clave.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("Usa entre 12 y 128 caracteres para la contraseña.");
        var salt = RandomNumberGenerator.GetBytes(24);
        var hash = await Task.Run(() => Derivar(clave, salt, Iteraciones));
        try
        {
            var db = await database.GetConnectionAsync();
            await db.RunInTransactionAsync(c =>
            {
                Exigir(RolUsuario.Gestor);
                ComprobarRecolector(c.Execute("""
                    UPDATE Usuarios SET Hash = ?, Salt = ?, Iteraciones = ?, VersionDerivacion = ?
                    WHERE Id = ? AND Rol = 'Recolector'
                    """, hash, salt, Iteraciones, Version, id));
                Revocar(c, id);
            });
        }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    private void Revocar(SQLiteConnection c, int id) =>
        c.Execute("UPDATE Sesiones SET RevocadaUtc = ? WHERE UsuarioId = ? AND RevocadaUtc IS NULL", Ahora.Ticks, id);

    private static void ComprobarRecolector(int filas)
    {
        if (filas != 1)
            throw new InvalidOperationException("La cuenta de recolector ya no está disponible.");
    }

    private static IdentidadUsuario Identificar(Usuario u) =>
        new(u.Id, u.Nombre, u.Correo, u.Telefono, Enum.Parse<RolUsuario>(u.Rol), u.Activo);
}

internal sealed class CorreoDuplicadoException : InvalidOperationException
{
    internal CorreoDuplicadoException() : base("Ya existe una cuenta con este correo.") { }
}
