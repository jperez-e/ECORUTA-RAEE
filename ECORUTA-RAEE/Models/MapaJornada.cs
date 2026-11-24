namespace ECORUTA_RAEE.Models;

internal sealed record ParadaMapa(int Id, int? Orden, string Codigo, string Direccion, string Zona,
    string Peso, string? Distancia, double? Latitud, double? Longitud)
{
    public string Titulo => Orden is int orden ? $"{orden}. {Codigo}" : Codigo;
    public bool Ubicada => Latitud.HasValue && Longitud.HasValue;
    public string Detalle => $"{Zona} · {Peso}";
    public string Tramo => Distancia ?? (Ubicada ? "" : "Sin coordenadas completas");
    public bool MostrarTramo => Tramo.Length > 0;
}

internal sealed record MapaJornada(string Titulo, string Estado, string Metodo, int SinCoordenadas,
    IReadOnlyList<ParadaMapa> Paradas)
{
    internal static MapaJornada Crear(JornadaRecoleccion jornada)
    {
        bool cancelada = jornada.Estado == JornadaRecoleccion.Cancelada;
        var ruta = jornada.RutaDesactualizada ? null : jornada.Ruta;
        var filas = ruta?.Paradas ?? jornada.Solicitudes.Select(s => new ParadaRuta(s, 0, null)).ToArray();
        var paradas = filas.Select(p =>
        {
            var s = p.Solicitud;
            bool ubicada = Coordenadas.SonValidas(s);
            return new ParadaMapa(s.Id, p.Orden > 0 ? p.Orden : null, s.CodigoSolicitud,
                s.DireccionReferencia, s.Zona, $"{s.PesoTotalEstimado:0.##} kg",
                p.DistanciaKm is double km ? $"≈ {km:0.##} km en línea recta" : null,
                ubicada ? s.Latitud : null, ubicada ? s.Longitud : null);
        }).ToArray();
        string metodo = ruta?.Metodo switch
        {
            MetodoRuta.VecinoMasCercano => "Orden por proximidad · Distancias aproximadas en línea recta",
            MetodoRuta.Antiguedad => "Orden por antigüedad · Los segmentos visibles son rectos",
            _ when jornada.RutaDesactualizada => cancelada
                ? "El orden histórico está desactualizado. Se muestran las ubicaciones actuales."
                : "Ruta desactualizada. Vuelve al detalle para regenerarla.",
            _ => cancelada ? "Sin orden histórico guardado." : "Vuelve al detalle para generar el orden de paradas."
        };
        return new(jornada.Nombre, cancelada ? "Cancelada · Consulta histórica"
            : $"Programada · {jornada.FechaProgramada:dd/MM/yyyy}", metodo,
            paradas.Count(p => !p.Ubicada), paradas);
    }
}
