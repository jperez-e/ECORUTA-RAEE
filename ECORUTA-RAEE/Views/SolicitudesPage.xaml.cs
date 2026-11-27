using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudesPage : ContentPage, IQueryAttributable
{
    private readonly SolicitudesViewModel _viewModel;
    private int? _inicioVisible;
    private bool _ocupada;

    public SolicitudesPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new SolicitudesViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Filtro", out var valor) && valor is FiltroGestion filtro)
        {
            _inicioVisible = null;
            _viewModel.AbrirFiltro(filtro);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        int? visible = _inicioVisible;
        var antes = _viewModel.Solicitudes;
        if (await OperacionBaseDatos.EjecutarAsync(_viewModel.CargarSolicitudesAsync) &&
            visible is int id && !ReferenceEquals(antes, _viewModel.Solicitudes) &&
            _viewModel.Solicitudes.FirstOrDefault(s => s.Id == id) is { } fila)
            Listado.ScrollTo(fila, position: ScrollToPosition.Start, animate: false);
    }

    private void Listado_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.FirstVisibleItemIndex >= 0 && e.FirstVisibleItemIndex < _viewModel.Solicitudes.Count)
            _inicioVisible = _viewModel.Solicitudes[e.FirstVisibleItemIndex].Id;
    }

    private async void Detalle_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: SolicitudGestion fila })
            await EjecutarAsync(() => Navigation.PushModalAsync(new ConsultaCiudadanaPage(fila.Id)));
    }

    private async void Editar_Clicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: SolicitudGestion fila })
            await EjecutarAsync(() => _viewModel.EditarCommand.ExecuteAsync(fila));
    }

    private async void Opciones_Clicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: SolicitudGestion fila })
            return;

        await EjecutarAsync(async () =>
        {
            const string residuos = "Ver equipos asociados";
            const string eliminar = "Eliminar solicitud";
            var opcion = await DisplayActionSheetAsync(fila.CodigoSolicitud, "Volver", eliminar, residuos);
            if (opcion == residuos)
                await _viewModel.VerResiduosCommand.ExecuteAsync(fila);
            else if (opcion == eliminar)
                await _viewModel.EliminarCommand.ExecuteAsync(fila);
        });
    }

    private async Task EjecutarAsync(Func<Task> accion)
    {
        if (_ocupada)
            return;
        _ocupada = true;
        try { await OperacionBaseDatos.EjecutarAsync(accion); }
        finally { _ocupada = false; }
    }
}
