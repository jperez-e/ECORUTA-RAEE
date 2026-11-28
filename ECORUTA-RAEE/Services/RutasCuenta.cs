using ECORUTA_RAEE.Models;
namespace ECORUTA_RAEE.Services;

internal static class RutasCuenta
{
    private static readonly HashSet<string> Administrativas = new(StringComparer.OrdinalIgnoreCase)
    {
        "solicitudes", "SolicitudesPage", "residuos", "ResiduosPage",
        "jornadas", "JornadasPage", "estadisticas", "EstadisticasPage", "recolectores", "RecolectoresPage",
        "ResiduoFormPage", "ResiduosSolicitudPage", "JornadaDetallePage"
    };

    internal static bool Permite(RolUsuario perfil, string ruta)
    {
        if (perfil == RolUsuario.Gestor)
            return true;
        var segmentos = Uri.UnescapeDataString(ruta.Split('?', '#')[0])
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segmentos.All(segmento => !Administrativas.Contains(segmento) &&
            (perfil != RolUsuario.Recolector ||
                !segmento.Equals("SolicitudFormPage", StringComparison.OrdinalIgnoreCase)));
    }
}
