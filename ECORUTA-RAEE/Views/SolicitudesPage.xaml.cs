using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudesPage : ContentPage
{
    private readonly SolicitudesViewModel _viewModel;

    public SolicitudesPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new SolicitudesViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await OperacionBaseDatos.EjecutarAsync(_viewModel.CargarSolicitudesAsync);
    }
}
