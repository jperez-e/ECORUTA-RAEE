using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.Views;

namespace ECORUTA_RAEE.ViewModels;

internal partial class JornadasViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<ZonaDisponible> zonas = Array.Empty<ZonaDisponible>();

    [ObservableProperty]
    private IReadOnlyList<JornadaRecoleccion> jornadas = Array.Empty<JornadaRecoleccion>();

    [ObservableProperty]
    private string cantidadTexto = string.Empty;

    [ObservableProperty]
    private string pesoTexto = string.Empty;

    [ObservableProperty]
    private string errorUmbrales = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySolicitudesSinZona))]
    private int sinZonaEspecifica;

    public bool HaySolicitudesSinZona => SinZonaEspecifica > 0;

    public async Task CargarAsync()
    {
        var panel = await JornadaService.Instancia.ObtenerPanelAsync();
        CantidadTexto = panel.Umbrales.Cantidad.ToString(CultureInfo.InvariantCulture);
        PesoTexto = panel.Umbrales.Peso.ToString(CultureInfo.InvariantCulture);
        Zonas = panel.Zonas;
        Jornadas = panel.Jornadas;
        SinZonaEspecifica = panel.SinZonaEspecifica;
        ErrorUmbrales = string.Empty;
    }

    [RelayCommand]
    private Task GuardarUmbrales() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        string pesoNormalizado = (PesoTexto ?? string.Empty).Trim().Replace(',', '.');
        if (!int.TryParse(CantidadTexto, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cantidad)
            || cantidad <= 0
            || !double.TryParse(pesoNormalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out double peso)
            || !double.IsFinite(peso) || peso <= 0)
        {
            ErrorUmbrales = "Introduce una cantidad entera y un peso, ambos mayores que cero.";
            return;
        }

        await JornadaService.Instancia.GuardarUmbralesAsync(new UmbralesJornada(cantidad, peso));
        await CargarAsync();
    });

    partial void OnCantidadTextoChanged(string value) => ErrorUmbrales = string.Empty;
    partial void OnPesoTextoChanged(string value) => ErrorUmbrales = string.Empty;

    [RelayCommand]
    private Task Previsualizar(ZonaDisponible zona) =>
        Shell.Current.GoToAsync(nameof(JornadaDetallePage), new ShellNavigationQueryParameters
        {
            ["Zona"] = zona.Zona
        });

    [RelayCommand]
    private Task Abrir(JornadaRecoleccion jornada) =>
        Shell.Current.GoToAsync(nameof(JornadaDetallePage), new ShellNavigationQueryParameters
        {
            ["JornadaId"] = jornada.Id
        });
}
