using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.Views;

public partial class HomePage : ContentPage
{
	public HomePage()
	{
		InitializeComponent();
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();

        TotalSolicitudesLabel.Text =
            SolicitudService.Instancia.Contar().ToString();
    }

    private async void NuevaSolicitud_Clicked(
        object sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync(
            nameof(SolicitudFormPage));
    }

    private async void VerSolicitudes_Clicked(
        object sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync("//solicitudes");
    }
}