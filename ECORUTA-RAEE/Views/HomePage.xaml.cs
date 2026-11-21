using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new HomeViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await OperacionBaseDatos.EjecutarAsync(_viewModel.ActualizarTotalAsync);
    }
}
