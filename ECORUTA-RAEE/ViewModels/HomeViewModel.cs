using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private int totalSolicitudes;

    public HomeViewModel()
    {
        ActualizarTotal();
    }

    public void ActualizarTotal()
    {
        TotalSolicitudes = SolicitudService.Instancia.Contar();
    }

    [RelayCommand]
    private async Task NuevaSolicitud()
    {
        await Shell.Current.GoToAsync(
            nameof(Views.SolicitudFormPage));
    }

    [RelayCommand]
    private async Task VerSolicitudes()
    {
        await Shell.Current.GoToAsync("//solicitudes");
    }
}