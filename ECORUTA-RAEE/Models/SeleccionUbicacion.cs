namespace ECORUTA_RAEE.Models;

internal sealed record SeleccionUbicacion(PuntoRecogida? Punto, string Zona, string? Sector)
{
    internal static SeleccionUbicacion? Crear(PuntoRecogida? punto, string? zona, string? sector)
    {
        if (zona is null || !ElegibilidadRecogida.Zonas.Contains(zona))
            return null;

        sector = zona == "Otra zona" ? sector?.Trim() : null;
        return zona == "Otra zona" && (string.IsNullOrEmpty(sector) || sector.Length > 100)
            ? null : new(punto, zona, sector);
    }
}
