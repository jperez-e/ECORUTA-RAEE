
using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class SolicitudesViewModel : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<SolicitudGestion> solicitudes = [];
    [ObservableProperty] private int filtroSeleccionado;
    public IReadOnlyList<string> Filtros { get; } =
    [
        "Todas las solicitudes", "Disponibles para jornada", "Incluidas en jornada activa",
        "Ubicación por completar", "Otra zona", "Programadas sin teléfono"
    ];

    internal void AbrirFiltro(FiltroGestion filtro)
    {
        TextoBusqueda = string.Empty;
        FiltroSeleccionado = (int)filtro;
    }

    partial void OnFiltroSeleccionadoChanged(int value) => AplicarFiltro();

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

    private IReadOnlyList<SolicitudGestion> _todas = [];
    private readonly SemaphoreSlim _carga = new(1, 1);

    public async Task CargarSolicitudesAsync()
    {
        await _carga.WaitAsync();
        try
        {
            _todas = await SolicitudService.Instancia.ObtenerGestionAsync();
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
        HayBusqueda = termino.Length > 0 || FiltroSeleccionado > 0;
        var filtro = (FiltroGestion)Math.Max(0, FiltroSeleccionado);
        var filtradas = todas.Where(s => s.Coincide(filtro) && (termino.Length == 0 ||
            s.CodigoSolicitud.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
            s.ZonaVisible.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
            s.DireccionReferencia.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
            s.DisponibilidadOperativa.Contains(termino, StringComparison.OrdinalIgnoreCase))).ToArray();

        if (!Solicitudes.Select(ValoresVisibles).SequenceEqual(filtradas.Select(ValoresVisibles)))
            Solicitudes = filtradas;

        ResumenBusqueda = $"{Solicitudes.Count} de {TotalSolicitudes} solicitudes";

        TituloListaVacia = HayBusqueda
            ? "No se encontraron solicitudes"
            : "Todavía no hay solicitudes";

        DescripcionListaVacia = HayBusqueda
            ? "Cambia el filtro o prueba otro código, zona o dirección."
            : "Cuando registres una recogida, aparecerá aquí para que puedas consultar sus datos.";
    }

    private static (int, string, string, string, string, long, double, bool, DateTime?) ValoresVisibles(SolicitudGestion s) =>
        (s.Id, s.CodigoSolicitud, s.ZonaVisible, s.DireccionReferencia, s.EstadoGestion, s.Unidades,
            s.PesoTotalEstimado, s.TieneTelefono, s.FechaProgramada);

    // CREATE: navegar al formulario vacío.
    [RelayCommand]
    private async Task NuevaSolicitud()
    {
        await Shell.Current.GoToAsync(
            nameof(ECORUTA_RAEE.Views.SolicitudFormPage),
            new ShellNavigationQueryParameters { ["VolverAListadoGestion"] = true });
    }

    // UPDATE: abrir el formulario con los datos seleccionados.
    [RelayCommand]
    private async Task Editar(SolicitudRecoleccion solicitud)
    {
        if (solicitud == null)
            return;

        var parametros = new Dictionary<string, object>
        {
            ["SolicitudId"] = solicitud.Id,
            ["VolverAListadoGestion"] = true
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
