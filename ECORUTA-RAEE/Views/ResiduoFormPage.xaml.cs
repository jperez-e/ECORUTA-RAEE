using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class ResiduoFormPage : ContentPage, IQueryAttributable
{
    private readonly ResiduoElectronicoViewModel _viewModel;
    private int? _residuoId;
    private int? _solicitudId;
    private bool _cargando;

    public ResiduoFormPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new ResiduoElectronicoViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ResiduoId", out var residuo) && residuo is int residuoId)
            _residuoId = residuoId;

        if (query.TryGetValue("SolicitudId", out var solicitud) && solicitud is int solicitudId)
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
                _viewModel.FormularioDisponible = await _viewModel.PrepararAsync(_residuoId, _solicitudId);
            });
        }
        finally
        {
            _cargando = false;
        }
    }
}
