using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudFormPage : ContentPage, IQueryAttributable
{
    private readonly SolicitudFormViewModel _viewModel;
    private int? _solicitudId;
    private bool _cargando;

    public SolicitudFormPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new SolicitudFormViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SolicitudId", out var valor) && valor is int solicitudId)
            _solicitudId = solicitudId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_cargando || _viewModel.FormularioDisponible)
            return;

        _cargando = true;
        try
        {
            await OperacionBaseDatos.EjecutarAsync(async () =>
            {
                if (_solicitudId is int id)
                {
                    _viewModel.FormularioDisponible = await _viewModel.CargarParaEditarAsync(id);
                }
                else
                {
                    _viewModel.PrepararNuevaSolicitud();
                    _viewModel.FormularioDisponible = true;
                }
            });
        }
        finally
        {
            _cargando = false;
        }
    }
}
