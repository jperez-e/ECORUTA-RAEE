using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudesPage : ContentPage
{
	public SolicitudesPage()
	{
		InitializeComponent();
	}
    protected override void OnAppearing()
    {
        base.OnAppearing();

        SolicitudesCollection.ItemsSource =
            SolicitudService.Instancia.ObtenerTodas();
    }

    private async void NuevaSolicitud_Clicked(
        object sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync(
            nameof(SolicitudFormPage));
    }
}