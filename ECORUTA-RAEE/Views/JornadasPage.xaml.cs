using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class JornadasPage : ContentPage
{
    private readonly JornadasViewModel _viewModel;

    public JornadasPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new JornadasViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.CargarPantallaAsync();
    }

    private void Configurar_Clicked(object? sender, EventArgs e)
    {
        Umbrales.IsVisible = !Umbrales.IsVisible;
        Configurar.Text = Umbrales.IsVisible ? "Ocultar configuración de umbrales" : "Configurar umbrales";
    }
}
