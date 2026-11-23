using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using SQLite;

namespace ECORUTA_RAEE.ViewModels;

internal partial class EstadisticasViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TienePanel))]
    private PanelAmbiental? panel;

    [ObservableProperty]
    private bool cargando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string error = string.Empty;

    public bool TienePanel => Panel is not null;
    public bool TieneError => Error.Length > 0;

    [RelayCommand]
    public async Task ActualizarAsync()
    {
        if (Cargando)
            return;

        Cargando = true;
        Error = string.Empty;
        try
        {
            Panel = await EstadisticasAmbientalesService.Instancia.ObtenerAsync();
        }
        catch (Exception ex) when (ex is SQLiteException or IOException or UnauthorizedAccessException
            or InvalidOperationException or TypeInitializationException or DllNotFoundException)
        {
            Debug.WriteLine(ex);
            Panel = null;
            Error = "No se pudo cargar el panel ambiental. Pulsa Actualizar para intentarlo de nuevo.";
        }
        finally
        {
            Cargando = false;
        }
    }
}
