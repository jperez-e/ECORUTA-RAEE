
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class SolicitudesViewModel : ObservableObject
{
    public ObservableCollection<SolicitudRecoleccion> Solicitudes
    {
        get;
    } = new();

    [ObservableProperty]
    private bool haySolicitudes;

    [ObservableProperty]
    private int totalSolicitudes;

    public SolicitudesViewModel()
    {
        CargarSolicitudes();
    }

    [RelayCommand]
    public void CargarSolicitudes()
    {
        Solicitudes.Clear();

        foreach (var solicitud in
                 SolicitudService.Instancia.ObtenerTodas())
        {
            Solicitudes.Add(solicitud);
        }

        TotalSolicitudes = Solicitudes.Count;
        HaySolicitudes = TotalSolicitudes > 0;
    }

    [RelayCommand]
    private async Task NuevaSolicitud()
    {
        await Shell.Current.GoToAsync(
            nameof(ECORUTA_RAEE.Views.SolicitudFormPage));
    }
}
