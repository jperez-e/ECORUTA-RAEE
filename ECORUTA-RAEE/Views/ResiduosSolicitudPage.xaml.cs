
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class ResiduosSolicitudPage :
    ContentPage, IQueryAttributable
{
    private readonly ResiduosSolicitudViewModel _viewModel;

    private int _solicitudId;

    public ResiduosSolicitudPage()
    {
        InitializeComponent();

        _viewModel = new ResiduosSolicitudViewModel();

        BindingContext = _viewModel;
    }

    // Recibir el ID de la solicitud
    public void ApplyQueryAttributes(
        IDictionary<string, object> query)
    {
        if (query.TryGetValue("SolicitudId", out var valor)
            && valor is int solicitudId)
        {
            _solicitudId = solicitudId;

            _viewModel.CargarResiduos(_solicitudId);
        }
    }

    // Actualizar datos al mostrar la pantalla
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_solicitudId > 0)
        {
            _viewModel.CargarResiduos(_solicitudId);
        }
    }
}
