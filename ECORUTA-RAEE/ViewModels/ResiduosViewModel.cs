
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.Views;

namespace ECORUTA_RAEE.ViewModels;

public partial class ResiduosViewModel : ObservableObject
{
    // Lista observable de residuos
    public ObservableCollection<ResiduoElectronico>
        Residuos
    { get; } = new();

    // Propiedades del listado
    [ObservableProperty]
    private bool hayResiduos;

    [ObservableProperty]
    private int totalResiduos;

    public ResiduosViewModel()
    {
        CargarResiduos();
    }

    // READ: Cargar los residuos registrados
    [RelayCommand]
    public void CargarResiduos()
    {
        Residuos.Clear();

        var registros =
            ResiduoElectronicoService.Instancia
                .ObtenerTodos();

        foreach (var residuo in registros)
        {
            Residuos.Add(residuo);
        }

        TotalResiduos = Residuos.Count;
        HayResiduos = TotalResiduos > 0;
    }

    // Navegar al formulario de creación
    [RelayCommand]
    private async Task NuevoResiduo()
    {
        // Primero debe existir una solicitud
        if (SolicitudService.Instancia.Contar() == 0)
        {
            await Shell.Current.DisplayAlertAsync(
                "No hay solicitudes",
                "Primero debes registrar una solicitud de recogida.",
                "Aceptar");

            return;
        }

        await Shell.Current.GoToAsync(
            nameof(ResiduoFormPage));
    }

    // Navegar al formulario de edición
    [RelayCommand]
    private async Task Editar(ResiduoElectronico residuo)
    {
        if (residuo == null)
            return;

        var parametros = new Dictionary<string, object>
        {
            ["Residuo"] = residuo
        };

        await Shell.Current.GoToAsync(
            nameof(ResiduoFormPage),
            parametros);
    }

    // DELETE: Eliminar desde el listado
    [RelayCommand]
    private async Task Eliminar(ResiduoElectronico residuo)
    {
        if (residuo == null)
            return;

        bool confirmar =
            await Shell.Current.DisplayAlertAsync(
                "Eliminar residuo",
                $"¿Deseas eliminar el residuo: {residuo.Descripcion}?",
                "Sí, eliminar",
                "Cancelar");

        if (!confirmar)
            return;

        bool eliminado =
            ResiduoElectronicoService.Instancia
                .Eliminar(residuo.Id);

        if (!eliminado)
        {
            await Shell.Current.DisplayAlertAsync(
                "Error",
                "No se pudo eliminar el residuo.",
                "Aceptar");

            return;
        }

        await Shell.Current.DisplayAlertAsync(
            "Residuo eliminado",
            "El registro se eliminó correctamente.",
            "Aceptar");

        CargarResiduos();
    }
}
