using ECORUTA_RAEE.Models;
using SQLite;

namespace ECORUTA_RAEE.Services;

internal sealed class EstadisticasAmbientalesService
{
    public static EstadisticasAmbientalesService Instancia { get; } = new();
    private EstadisticasAmbientalesService() { }

    public async Task<PanelAmbiental> ObtenerAsync()
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        PanelAmbiental panel = null!;
        await db.RunInTransactionAsync(connection => panel = LeerPanel(connection));
        return panel;
    }

    internal async Task<(PanelAmbiental Panel, IReadOnlyList<AlertaGestion> Alertas)> ObtenerGestionAsync()
    {
        var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
        PanelAmbiental panel = null!;
        IReadOnlyList<AlertaGestion> alertas = [];
        await db.RunInTransactionAsync(connection =>
        {
            panel = LeerPanel(connection);
            alertas = AlertaGestion.Crear(panel, JornadaService.LeerPortadaCampo(connection));
        });
        return (panel, alertas);
    }

    private static PanelAmbiental LeerPanel(SQLiteConnection connection)
    {
        var totalesSolicitudes = connection.Query<TotalesSolicitudesZona>("""
            SELECT Zona, COUNT(*) AS Cantidad,
                   COUNT(CASE WHEN Estado = 'Pendiente' THEN 1 END) AS Pendientes,
                   SUM(PesoTotalEstimado) AS Peso
            FROM Solicitudes GROUP BY Zona
            """);
        var categorias = connection.Query<CategoriaAmbiental>("""
            SELECT Categoria, COUNT(*) AS Registros, SUM(Cantidad) AS Unidades, SUM(PesoEstimado) AS Peso
            FROM Residuos GROUP BY Categoria
            """)
            .GroupBy(r => r.Categoria.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grupo => new CategoriaAmbiental
            {
                Categoria = string.IsNullOrWhiteSpace(grupo.Key) ? "Sin categoría" : grupo.Key,
                Registros = grupo.Sum(r => r.Registros),
                Unidades = grupo.Sum(r => r.Unidades),
                Peso = grupo.Sum(r => r.Peso)
            })
            .OrderByDescending(c => c.Unidades).ThenBy(c => c.Categoria).ToList();
        long unidades = categorias.Sum(c => c.Unidades);
        foreach (var categoria in categorias)
            categoria.ProporcionEquipos = unidades > 0 ? (double)categoria.Unidades / unidades : 0;

        var jornadas = connection.Query<ResumenJornadasAmbiental>("""
            SELECT COUNT(CASE WHEN Estado = ? THEN 1 END) AS Programadas,
                   COUNT(CASE WHEN Estado = ? THEN 1 END) AS Canceladas,
                   (SELECT COUNT(*) FROM JornadaSolicitudes WHERE Activa = 1) AS SolicitudesAsignadas
            FROM Jornadas
            """, JornadaRecoleccion.Programada, JornadaRecoleccion.Cancelada).Single();

        var (umbrales, disponibilidad, _, sinUbicacion) = JornadaService.LeerDisponibilidad(connection);
        var porZona = disponibilidad.ToDictionary(z => z.Zona, StringComparer.OrdinalIgnoreCase);
        var zonasIdentificadas = JornadaService.AgruparZonas(
            totalesSolicitudes.Where(z => JornadaService.EsZonaEspecifica(z.Zona))).ToList();
        double pesoIdentificado = zonasIdentificadas.Sum(z => z.Peso);
        var zonas = zonasIdentificadas.Select(z => new ZonaAmbiental(
                z.Zona, z.Cantidad, z.Peso, pesoIdentificado > 0 ? z.Peso / pesoIdentificado : 0,
                porZona.GetValueOrDefault(z.Zona) ?? new ZonaDisponible(z.Zona, 0, 0, umbrales)))
            .OrderByDescending(z => z.Peso).ThenBy(z => z.Zona).ToList();
        var alertas = zonas.Where(z => z.Nivel != NivelAlertaZona.SinAlerta)
            .OrderByDescending(z => z.Nivel).ThenByDescending(z => z.Avance).ThenBy(z => z.Zona).ToList();
        var sinZona = totalesSolicitudes.Where(z => !JornadaService.EsZonaEspecifica(z.Zona)).ToList();
        var indicadores = new IndicadoresAmbientales(
            totalesSolicitudes.Sum(z => z.Cantidad), totalesSolicitudes.Sum(z => z.Pendientes),
            totalesSolicitudes.Sum(z => z.Peso), categorias.Sum(c => c.Registros), unidades);

        return new PanelAmbiental(indicadores, jornadas, umbrales, zonas, alertas, categorias,
            sinZona.Sum(z => z.Cantidad), sinZona.Sum(z => z.Peso), sinUbicacion,
            disponibilidad.Sum(z => z.Cantidad), disponibilidad.Sum(z => z.Peso));
    }
}
