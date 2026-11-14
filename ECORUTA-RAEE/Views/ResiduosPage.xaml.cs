
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class ResiduosPage : ContentPage
{
    private readonly ResiduosViewModel _viewModel;

    public ResiduosPage()
    {
        InitializeComponent();

        BindingContext = _viewModel =
            new ResiduosViewModel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.CargarResiduos();
    }
}