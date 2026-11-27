namespace ECORUTA_RAEE.Models;

internal sealed record ResumenJornadaCampo(int Id, string Zona, DateTime Fecha, int Paradas,
    long Unidades, double Peso, string EstadoOrden);

internal sealed record ParadaCampo(SolicitudRecoleccion Solicitud, long Unidades)
{
    internal static string UnidadesVisibles(long cantidad) =>
        $"{cantidad} {(cantidad == 1 ? "unidad" : "unidades")}";
}

internal sealed record ConsultaCampo(ResumenJornadaCampo Resumen, bool OrdenVigente,
    IReadOnlyList<ParadaCampo> Paradas, IReadOnlyList<ResiduoElectronico> Equipos)
{
    internal int Indice(int solicitudId)
    {
        for (int i = 0; i < Paradas.Count; i++)
            if (Paradas[i].Solicitud.Id == solicitudId)
                return i;
        return -1;
    }

    internal int? Adyacente(int solicitudId, int desplazamiento)
    {
        int indice = Indice(solicitudId);
        int destino = indice + desplazamiento;
        return indice >= 0 && destino >= 0 && destino < Paradas.Count
            ? Paradas[destino].Solicitud.Id : null;
    }
}
