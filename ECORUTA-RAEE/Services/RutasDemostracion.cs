namespace ECORUTA_RAEE.Services;

internal static class RutasDemostracion
{
    private static readonly HashSet<string> Administrativas = new(StringComparer.OrdinalIgnoreCase)
    {
        "solicitudes", "SolicitudesPage", "residuos", "ResiduosPage",
        "jornadas", "JornadasPage", "estadisticas", "EstadisticasPage",
        "ResiduoFormPage", "ResiduosSolicitudPage", "JornadaDetallePage"
    };

    internal static bool Permite(PerfilDemostracion perfil, string ruta)
    {
        if (perfil == PerfilDemostracion.Gestor)
            return true;
        var segmentos = Uri.UnescapeDataString(ruta.Split('?', '#')[0])
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segmentos.All(segmento => !Administrativas.Contains(segmento) &&
            (perfil != PerfilDemostracion.Recolector ||
                !segmento.Equals("SolicitudFormPage", StringComparison.OrdinalIgnoreCase)));
    }
}
