namespace ECORUTA_RAEE.Models;

internal enum EstadoOrdenCampo { Pendiente, Vigente, Desactualizado }

internal sealed record ResumenJornadaCampo(int Id, string Zona, DateTime Fecha, int Paradas,
    long Unidades, double Peso, EstadoOrdenCampo Orden, int UbicacionesIncompletas, int SinTelefono)
{
    internal string EstadoOrden => Orden switch
    {
        EstadoOrdenCampo.Vigente => "Orden de paradas disponible",
        EstadoOrdenCampo.Desactualizado => "Orden desactualizado",
        _ => "Orden de paradas pendiente"
    };
}

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
