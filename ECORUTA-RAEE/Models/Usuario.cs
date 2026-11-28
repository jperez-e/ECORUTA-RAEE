
using SQLite;

namespace ECORUTA_RAEE.Models;

internal enum RolUsuario { Ciudadano, Gestor, Recolector }

[Table("Usuarios")]
internal sealed class Usuario
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    [Unique]
    public string Correo { get; set; } = "";
    public string? Telefono { get; set; }
    public byte[] Hash { get; set; } = [];
    public byte[] Salt { get; set; } = [];
    public int Iteraciones { get; set; }
    public int VersionDerivacion { get; set; }
    public string Rol { get; set; } = "";
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}

internal sealed record IdentidadUsuario(int Id, string Nombre, string Correo, string? Telefono,
    RolUsuario Rol, bool Activo);

internal sealed record DatosCuenta(string Nombre, string Correo, string? Telefono);
