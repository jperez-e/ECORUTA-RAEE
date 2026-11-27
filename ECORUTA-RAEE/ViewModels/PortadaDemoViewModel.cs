using CommunityToolkit.Mvvm.ComponentModel;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

internal sealed class PortadaDemoViewModel(PerfilDemostracion perfil) : ObservableObject
{
    internal IReadOnlyList<SolicitudRecoleccion> Solicitudes { get; private set; } = [];
    internal IReadOnlyList<SolicitudCiudadana> VistaCiudadana { get; private set; } = [];
    internal IReadOnlyList<ResumenJornadaCampo> PortadaCampo { get; private set; } = [];
    internal IReadOnlyList<AlertaGestion> AlertasGestion { get; private set; } = [];
    internal PanelAmbiental? Panel { get; private set; }
    private ILookup<int, ResiduoElectronico>? _equipos;

    internal IEnumerable<ResiduoElectronico> Equipos(int id) => _equipos?[id] ?? [];

    internal Task<bool> CargarAsync() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        switch (perfil)
        {
            case PerfilDemostracion.Ciudadano:
                VistaCiudadana = await SolicitudService.Instancia.ObtenerVistaCiudadanaAsync();
                Solicitudes = VistaCiudadana.Select(f => f.Solicitud).ToArray();
                _equipos = VistaCiudadana.SelectMany(f => f.Equipos).ToLookup(e => e.SolicitudId);
                break;
            case PerfilDemostracion.Gestor:
                (Panel, AlertasGestion) = await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync();
                break;
            case PerfilDemostracion.Recolector:
                PortadaCampo = await JornadaService.Instancia.ObtenerPortadaCampoAsync();
                break;
        }
    });
}
