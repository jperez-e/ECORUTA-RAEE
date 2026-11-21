using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private int totalSolicitudes;

    public async Task ActualizarTotalAsync()
    {
        TotalSolicitudes = await SolicitudService.Instancia.ContarAsync();
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
