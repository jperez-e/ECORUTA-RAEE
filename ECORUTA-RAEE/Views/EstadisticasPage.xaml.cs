using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class EstadisticasPage : ContentPage
{
    private readonly EstadisticasViewModel _viewModel = new();

    public EstadisticasPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.ActualizarAsync();
    }
}
