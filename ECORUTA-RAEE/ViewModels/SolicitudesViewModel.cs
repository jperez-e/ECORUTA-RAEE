
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    // Texto introducido en el buscador (enlazado con SearchBar).
    [ObservableProperty]
    private string textoBusqueda = string.Empty;

    // Mensajes para distinguir una lista vacía de una búsqueda sin coincidencias.
    [ObservableProperty]
    private string tituloListaVacia = "Todavía no hay solicitudes";

    [ObservableProperty]
    private string descripcionListaVacia =
        "Cuando registres una recogida, aparecerá aquí para que puedas consultar sus datos.";

    [ObservableProperty]
    private bool hayBusqueda;

    [ObservableProperty]
    private string resumenBusqueda = string.Empty;

    private List<SolicitudRecoleccion> _todas = new();
    private readonly SemaphoreSlim _carga = new(1, 1);

    public async Task CargarSolicitudesAsync()
    {
        await _carga.WaitAsync();
        try
        {
            _todas = await SolicitudService.Instancia.ObtenerTodasAsync();
            AplicarFiltro();
        }
        finally
        {
            _carga.Release();
        }
    }

    // CommunityToolkit.Mvvm llama a este método cuando cambia TextoBusqueda.
    partial void OnTextoBusquedaChanged(string value)
    {
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        // No se modifican los datos del servicio: solo la colección visible.
        var todas = _todas;
        string termino = TextoBusqueda?.Trim() ?? string.Empty;

        TotalSolicitudes = todas.Count;
        HaySolicitudes = TotalSolicitudes > 0;
        HayBusqueda = termino.Length > 0;

        var filtradas = HayBusqueda
            ? todas.Where(s =>
                (s.CodigoSolicitud?.Contains(termino, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Zona?.Contains(termino, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.DireccionReferencia?.Contains(termino, StringComparison.OrdinalIgnoreCase) ?? false))
            : todas.AsEnumerable();

        Solicitudes.Clear();

        foreach (var solicitud in filtradas)
        {
            Solicitudes.Add(solicitud);
        }

        ResumenBusqueda = HayBusqueda
            ? $"{Solicitudes.Count} de {TotalSolicitudes} solicitudes encontradas"
            : string.Empty;

        TituloListaVacia = HayBusqueda
            ? "No se encontraron solicitudes"
            : "Todavía no hay solicitudes";

        DescripcionListaVacia = HayBusqueda
            ? "Prueba con otro código, zona o dirección, o borra el texto de búsqueda."
            : "Cuando registres una recogida, aparecerá aquí para que puedas consultar sus datos.";
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
            ["SolicitudId"] = solicitud.Id
        };

        await Shell.Current.GoToAsync(
            nameof(ECORUTA_RAEE.Views.SolicitudFormPage),
            parametros);
    }

    // DELETE: eliminar directamente desde la lista, con advertencia
    // de eliminación en cascada cuando existan residuos asociados.
    [RelayCommand]
    private Task Eliminar(SolicitudRecoleccion solicitud) => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        if (solicitud == null)
            return;

        var existente = await SolicitudService.Instancia.ObtenerPorIdAsync(solicitud.Id);

        if (existente == null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible",
                "La solicitud ya no existe.",
                "Aceptar");

            await CargarSolicitudesAsync();
            return;
        }

        int cantidadResiduos = await ResiduoElectronicoService.Instancia
            .ContarPorSolicitudIdAsync(solicitud.Id);

        string mensaje = cantidadResiduos > 0
            ? $"¿Deseas eliminar la solicitud {solicitud.CodigoSolicitud}?\n\n" +
              $"También se eliminarán sus {cantidadResiduos} residuos electrónicos asociados.\n\n" +
              "Esta acción no se puede deshacer."
            : $"¿Deseas eliminar la solicitud {solicitud.CodigoSolicitud}?\n\n" +
              "Esta acción no se puede deshacer.";

        if (await JornadaService.Instancia.TieneJornadasAsync(solicitud.Id))
            mensaje += "\n\nTambién se quitará de sus jornadas. Si era la última solicitud de una jornada activa, esa jornada se cancelará.";

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Eliminar solicitud",
            mensaje,
            "Sí, eliminar",
            "Cancelar");

        if (!confirmar)
            return;

        // El servicio ya se encarga de eliminar los residuos asociados.
        bool eliminado = await SolicitudService.Instancia.EliminarAsync(solicitud.Id);

        if (!eliminado)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible",
                "La solicitud ya no existe.",
                "Aceptar");

            return;
        }

        await CargarSolicitudesAsync();

        await Shell.Current.DisplayAlertAsync(
            "Solicitud eliminada",
            "La solicitud y sus residuos asociados se eliminaron correctamente.",
            "Aceptar");
    });

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
