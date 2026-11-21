using System.Security.Cryptography;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.Services;

internal static class PlanificadorRutas
{
    internal static PlanificacionRuta Calcular(IEnumerable<SolicitudRecoleccion> solicitudes)
    {
        var pendientes = solicitudes.OrderBy(s => s.FechaRegistro)
            .ThenBy(s => s.CodigoSolicitud, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id)
            .ToList();
        if (pendientes.Count == 0)
            throw new ArgumentException("Se necesita al menos una solicitud.", nameof(solicitudes));

        if (pendientes.Any(s => !Coordenadas.SonValidas(s)))
            return new PlanificacionRuta(MetodoRuta.Antiguedad,
                pendientes.Select((s, i) => new ParadaRuta(s, i + 1, null)).ToList());

        var paradas = new List<ParadaRuta>(pendientes.Count) { new(pendientes[0], 1, null) };
        pendientes.RemoveAt(0);
        while (pendientes.Count > 0)
        {
            var anterior = paradas[^1].Solicitud;
            int indiceCercano = 0;
            double distanciaMinima = double.PositiveInfinity;
            for (int i = 0; i < pendientes.Count; i++)
            {
                var candidata = pendientes[i];
                double distancia = DistanciaHaversineKm(anterior.Latitud!.Value, anterior.Longitud!.Value,
                    candidata.Latitud!.Value, candidata.Longitud!.Value);
                // El orden inicial resuelve los empates por fecha, código e ID.
                if (distancia < distanciaMinima)
                {
                    indiceCercano = i;
                    distanciaMinima = distancia;
                }
            }

            paradas.Add(new ParadaRuta(pendientes[indiceCercano], paradas.Count + 1, distanciaMinima));
            pendientes.RemoveAt(indiceCercano);
        }

        return new PlanificacionRuta(MetodoRuta.VecinoMasCercano, paradas);
    }

    internal static double DistanciaHaversineKm(double latitudA, double longitudA, double latitudB, double longitudB)
    {
        const double radianes = Math.PI / 180;
        const double radioMedioKm = 6371.0088;
        double senoLatitud = Math.Sin((latitudB - latitudA) * radianes / 2);
        double senoLongitud = Math.Sin((longitudB - longitudA) * radianes / 2);
        double a = senoLatitud * senoLatitud + Math.Cos(latitudA * radianes) * Math.Cos(latitudB * radianes)
            * senoLongitud * senoLongitud;
        return 2 * radioMedioKm * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    internal static string CalcularHuella(IEnumerable<SolicitudRecoleccion> solicitudes)
    {
        // El peso se consulta en vivo. Estos valores identifican las paradas y su orden.
        using var datos = new MemoryStream();
        using var escritor = new BinaryWriter(datos);
        foreach (var solicitud in solicitudes.OrderBy(s => s.Id))
        {
            escritor.Write(solicitud.Id);
            escritor.Write(solicitud.FechaRegistro.Ticks);
            escritor.Write(solicitud.CodigoSolicitud);
            escritor.Write(solicitud.Zona);
            escritor.Write(solicitud.DireccionReferencia);
            EscribirCoordenada(escritor, solicitud.Latitud);
            EscribirCoordenada(escritor, solicitud.Longitud);
        }
        escritor.Flush();
        return Convert.ToHexString(SHA256.HashData(datos.GetBuffer().AsSpan(0, (int)datos.Length)));
    }

    private static void EscribirCoordenada(BinaryWriter escritor, double? valor)
    {
        escritor.Write(valor.HasValue);
        if (valor is double numero)
            escritor.Write(numero);
    }
}
