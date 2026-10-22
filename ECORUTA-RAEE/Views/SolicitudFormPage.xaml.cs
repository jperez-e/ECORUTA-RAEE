
using System.Globalization;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudFormPage : ContentPage
{
	public SolicitudFormPage()
	{
		InitializeComponent();

        ZonaPicker.ItemsSource = new List<string>
        {
            "Herrera",
            "Los Alcarrizos",
            "Manoguayabo",
            "Pantoja",
            "Pedro Brand",
            "Otra zona"
        };
    }

    private async void Guardar_Clicked(
        object sender,
        EventArgs e)
    {
        MensajeError.IsVisible = false;

        string direccion = DireccionEditor.Text?.Trim() ?? "";
        string? zona = ZonaPicker.SelectedItem?.ToString();
        string pesoTexto = PesoEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(zona))
        {
            MostrarError("Debes seleccionar una zona.");
            return;
        }

        if (string.IsNullOrWhiteSpace(direccion))
        {
            MostrarError("Debes introducir una dirección o referencia.");
            return;
        }

        // Admite punto decimal o coma decimal.
        string pesoNormalizado = pesoTexto.Replace(',', '.');

        bool pesoValido = double.TryParse(
            pesoNormalizado,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out double peso);

        if (!pesoValido || peso <= 0)
        {
            MostrarError("Introduce un peso válido mayor que cero.");
            return;
        }

        var solicitud = new SolicitudRecoleccion
        {
            Zona = zona,
            DireccionReferencia = direccion,
            PesoTotalEstimado = peso,
            Estado = "Pendiente"
        };

        SolicitudService.Instancia.Crear(solicitud);

        await DisplayAlertAsync(
            "Solicitud creada",
            $"Se registró la solicitud {solicitud.CodigoSolicitud}.",
            "Aceptar");

        await Shell.Current.GoToAsync("//solicitudes");
    }

    private void MostrarError(string mensaje)
    {
        MensajeError.Text = mensaje;
        MensajeError.IsVisible = true;
    }

    private async void Cancelar_Clicked(
        object sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}