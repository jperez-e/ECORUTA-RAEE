
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.Views;

namespace ECORUTA_RAEE.ViewModels;

public partial class ResiduosViewModel : ObservableObject
{
    // Lista observable de residuos que se muestra en pantalla.
    public ObservableCollection<ResiduoElectronico> Residuos { get; } = new();

    [ObservableProperty]
    private bool hayResiduos;

    [ObservableProperty]
    private int totalResiduos;

    // BUSCADOR
    [ObservableProperty]
    private string textoBusqueda = string.Empty;

    [ObservableProperty]
    private bool hayBusqueda;

    [ObservableProperty]
    private string resumenBusqueda = string.Empty;

    [ObservableProperty]
    private string tituloListaVacia = "No hay residuos registrados";

    [ObservableProperty]
    private string descripcionListaVacia =
        "Registra los equipos electrónicos que serán entregados en una solicitud de recogida.";

    private List<ResiduoElectronico> _todos = new();
    private Dictionary<int, string> _codigos = new();
    private readonly SemaphoreSlim _carga = new(1, 1);

    public async Task CargarResiduosAsync()
    {
        await _carga.WaitAsync();
        try
        {
            (_todos, _codigos) = await ResiduoElectronicoService.Instancia.ObtenerListadoAsync();
            AplicarFiltro();
        }
        finally
        {
            _carga.Release();
        }
    }

    // Se ejecuta automáticamente cuando cambia el texto del SearchBar.
    partial void OnTextoBusquedaChanged(string value)
    {
        AplicarFiltro();
    }

    // FILTRADO EN TIEMPO REAL
    private void AplicarFiltro()
    {
        var todos = _todos;

        string termino = TextoBusqueda?.Trim() ?? string.Empty;

        // El contador general siempre muestra todos los registros.
        TotalResiduos = todos.Count;
        HayResiduos = TotalResiduos > 0;
        HayBusqueda = !string.IsNullOrWhiteSpace(termino);

        var filtrados = HayBusqueda
            ? todos.Where(r => CoincideConBusqueda(r, termino))
            : todos.AsEnumerable();

        Residuos.Clear();

        foreach (var residuo in filtrados)
        {
            Residuos.Add(residuo);
        }

        ResumenBusqueda = HayBusqueda
            ? $"{Residuos.Count} de {TotalResiduos} residuos encontrados"
            : string.Empty;

        TituloListaVacia = HayBusqueda
            ? "No se encontraron residuos"
            : "No hay residuos registrados";

        DescripcionListaVacia = HayBusqueda
            ? "Prueba con otra categoría, descripción o código de solicitud."
            : "Registra los equipos electrónicos que serán entregados en una solicitud de recogida.";
    }

    // Comprueba si un residuo coincide con el texto introducido.
    private bool CoincideConBusqueda(
        ResiduoElectronico residuo,
        string termino)
    {
        _codigos.TryGetValue(residuo.SolicitudId, out var codigo);

        bool coincideCategoria =
            residuo.Categoria?.Contains(
                termino,
                StringComparison.OrdinalIgnoreCase) ?? false;

        bool coincideDescripcion =
            residuo.Descripcion?.Contains(
                termino,
                StringComparison.OrdinalIgnoreCase) ?? false;

        bool coincideSolicitudId =
            residuo.SolicitudId.ToString().Contains(
                termino,
                StringComparison.OrdinalIgnoreCase);

        bool coincideCodigoSolicitud =
            codigo?.Contains(
                termino,
                StringComparison.OrdinalIgnoreCase) ?? false;

        return coincideCategoria ||
               coincideDescripcion ||
               coincideSolicitudId ||
               coincideCodigoSolicitud;
    }

    // CREATE: navegar al formulario de creación.
    [RelayCommand]
    private Task NuevoResiduo() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        // Primero debe existir una solicitud.
        if (await SolicitudService.Instancia.ContarAsync() == 0)
        {
            await Shell.Current.DisplayAlertAsync(
                "No hay solicitudes",
                "Primero debes registrar una solicitud de recogida.",
                "Aceptar");

            return;
        }

        await Shell.Current.GoToAsync(
            nameof(ResiduoFormPage));
    });

    // UPDATE: abrir el formulario con el residuo seleccionado.
    [RelayCommand]
    private async Task Editar(ResiduoElectronico residuo)
    {
        if (residuo == null)
            return;

        var parametros = new Dictionary<string, object>
        {
            ["ResiduoId"] = residuo.Id
        };

        await Shell.Current.GoToAsync(
            nameof(ResiduoFormPage),
            parametros);
    }

    // DELETE: eliminar desde el listado con confirmación.
    [RelayCommand]
    private Task Eliminar(ResiduoElectronico residuo) => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        if (residuo == null)
            return;

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Eliminar residuo",
            $"¿Deseas eliminar el residuo: {residuo.Descripcion}?",
            "Sí, eliminar",
            "Cancelar");

        if (!confirmar)
            return;

        bool eliminado = await ResiduoElectronicoService.Instancia
            .EliminarAsync(residuo.Id);

        if (!eliminado)
        {
            await Shell.Current.DisplayAlertAsync(
                "Residuo no disponible",
                "El residuo ya no existe.",
                "Aceptar");

            return;
        }

        // Actualizar la lista respetando el filtro actual.
        await CargarResiduosAsync();

        await Shell.Current.DisplayAlertAsync(
            "Residuo eliminado",
            "El registro se eliminó correctamente.",
            "Aceptar");
    });
}
