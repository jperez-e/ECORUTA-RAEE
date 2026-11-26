namespace ECORUTA_RAEE.Services;

internal enum PerfilDemostracion { Ciudadano, Gestor, Recolector }

internal static class SesionDemostracion
{
    public const string Aviso = "Modo demostración · Datos de este dispositivo";
    public static PerfilDemostracion? Perfil { get; internal set; }
}
