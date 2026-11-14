using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class ResiduosSolicitudViewModel : ObservableObject
{
    // Lista de residuos de una solicitud específica
    public ObservableCollection<ResiduoElectronico> Residuos
    { get; } = new();

    [ObservableProperty]
    private string codigoSolicitud = string.Empty;

    [ObservableProperty]
    private string zona = string.Empty;

    [ObservableProperty]
    private int totalRegistros;

    [ObservableProperty]
    private int totalEquipos;

    [ObservableProperty]
    private double pesoTotal;

    // Identificador de la solicitud que estamos consultando
    private int _solicitudId;


    // Consultar los residuos asociados a una solicitud
    public void CargarResiduos(int solicitudId)
    {
        _solicitudId = solicitudId;
        Residuos.Clear();

        // Buscar la solicitud
        var solicitud = SolicitudService.Instancia
            .ObtenerPorId(solicitudId);

        if (solicitud == null)
        {
            CodigoSolicitud = "Solicitud no disponible";
            Zona = string.Empty;
            TotalRegistros = 0;
            TotalEquipos = 0;
            PesoTotal = 0;
            return;
        }

        CodigoSolicitud = solicitud.CodigoSolicitud;
        Zona = solicitud.Zona;

        // Obtener únicamente los residuos de esta solicitud
        var registros = ResiduoElectronicoService.Instancia
            .ObtenerPorSolicitudId(solicitudId);

        foreach (var residuo in registros)
        {
            Residuos.Add(residuo);
        }

        // Calcular estadísticas
        TotalRegistros = Residuos.Count;

        TotalEquipos = Residuos.Sum(
            r => r.Cantidad);

        PesoTotal = Residuos.Sum(
            r => r.PesoEstimado);
    }

    // Agregar un equipo a la solicitud actual
    [RelayCommand]
    private async Task AgregarEquipo()
    {
        if (_solicitudId <= 0 ||
            SolicitudService.Instancia
                .ObtenerPorId(_solicitudId) == null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible",
                "No se encontró la solicitud de recogida.",
                "Aceptar");

            return;
        }

        var parametros = new Dictionary<string, object>
        {
            ["SolicitudId"] = _solicitudId
        };

        await Shell.Current.GoToAsync(
            nameof(ECORUTA_RAEE.Views.ResiduoFormPage),
            parametros);
    }

}
