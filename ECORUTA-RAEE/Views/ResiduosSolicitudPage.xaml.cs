using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class ResiduosSolicitudPage : ContentPage, IQueryAttributable
{
    private readonly ResiduosSolicitudViewModel _viewModel;
    private int _solicitudId;

    public ResiduosSolicitudPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new ResiduosSolicitudViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SolicitudId", out var valor) && valor is int solicitudId)
            _solicitudId = solicitudId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_solicitudId > 0)
            await OperacionBaseDatos.EjecutarAsync(() => _viewModel.CargarResiduosAsync(_solicitudId));
    }
}
