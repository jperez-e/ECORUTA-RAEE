
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class SolicitudesViewModel : ObservableObject
{
    public ObservableCollection<SolicitudRecoleccion> Solicitudes { get; } = new();

    [ObservableProperty]
    private bool haySolicitudes;

    [ObservableProperty]
    private int totalSolicitudes;

    public SolicitudesViewModel()
    {
        CargarSolicitudes();
    }

    // READ
    [RelayCommand]
    public void CargarSolicitudes()
    {
        Solicitudes.Clear();

        foreach (var solicitud in SolicitudService.Instancia.ObtenerTodas())
        {
            Solicitudes.Add(solicitud);
        }

        TotalSolicitudes = Solicitudes.Count;
        HaySolicitudes = TotalSolicitudes > 0;
    }

    // CREATE: navegar al formulario vacío.
    [RelayCommand]
    private async Task NuevaSolicitud()
    {
        await Shell.Current.GoToAsync(
            nameof(ECORUTA_RAEE.Views.SolicitudFormPage));
    }

    // UPDATE: abrir el formulario con los datos seleccionados.
    [RelayCommand]
    private async Task Editar(SolicitudRecoleccion solicitud)
    {
        if (solicitud == null)
            return;

        var parametros = new Dictionary<string, object>
        {
            ["Solicitud"] = solicitud
        };

        await Shell.Current.GoToAsync(
            nameof(ECORUTA_RAEE.Views.SolicitudFormPage),
            parametros);
    }

    // DELETE: eliminar desde la lista con advertencia.
    [RelayCommand]
    private async Task Eliminar(SolicitudRecoleccion solicitud)
    {
        if (solicitud == null)
            return;

        var existente = SolicitudService.Instancia
            .ObtenerPorId(solicitud.Id);

        if (existente == null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible",
                "La solicitud ya no existe.",
                "Aceptar");

            CargarSolicitudes();
            return;
        }

        // Consultar residuos asociados.
        int cantidadResiduos = ResiduoElectronicoService.Instancia
            .ObtenerPorSolicitudId(solicitud.Id).Count;

        string mensaje = cantidadResiduos > 0
            ? $"¿Deseas eliminar la solicitud {solicitud.CodigoSolicitud}?\n\n" +
              $"También se eliminarán sus {cantidadResiduos} residuos electrónicos asociados.\n\n" +
              "Esta acción no se puede deshacer."
            : $"¿Deseas eliminar la solicitud {solicitud.CodigoSolicitud}?\n\n" +
              "Esta acción no se puede deshacer.";

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Eliminar solicitud",
            mensaje,
            "Sí, eliminar",
            "Cancelar");

        if (!confirmar)
            return;

        // El servicio realiza la eliminación en cascada.
        bool eliminado = SolicitudService.Instancia
            .Eliminar(solicitud.Id);

        if (!eliminado)
        {
            await Shell.Current.DisplayAlertAsync(
                "Error",
                "No se pudo eliminar la solicitud.",
                "Aceptar");

            return;
        }

        // Actualizar listado y contador.
        CargarSolicitudes();

        await Shell.Current.DisplayAlertAsync(
            "Solicitud eliminada",
            "La solicitud y sus residuos asociados se eliminaron correctamente.",
            "Aceptar");
    }

    // Consultar los residuos asociados a una solicitud.
    [RelayCommand]
    private async Task VerResiduos(SolicitudRecoleccion solicitud)
    {
        if (solicitud == null)
            return;

        var parametros = new Dictionary<string, object>
        {
            ["SolicitudId"] = solicitud.Id
        };

        await Shell.Current.GoToAsync(
            nameof(ECORUTA_RAEE.Views.ResiduosSolicitudPage),
            parametros);
    }
}
