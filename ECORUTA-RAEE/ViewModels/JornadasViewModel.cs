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
    private IReadOnlyList<AvanceZona> zonas = Array.Empty<AvanceZona>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeInteractuar))]
    private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAvisoUmbrales))]
    private string avisoUmbrales = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<JornadaRecoleccion> jornadas = Array.Empty<JornadaRecoleccion>();

    [ObservableProperty]
    private string cantidadTexto = string.Empty;

    [ObservableProperty]
    private string pesoTexto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayErrorUmbrales))]
    private string errorUmbrales = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySolicitudesSinZona))]
    private int sinZonaEspecifica;

    public bool HaySolicitudesSinZona => SinZonaEspecifica > 0;
    public bool HayAvisoUmbrales => AvisoUmbrales.Length > 0;
    public bool HayErrorUmbrales => ErrorUmbrales.Length > 0;
    public bool PuedeInteractuar => !Ocupado;

    public Task CargarPantallaAsync() => EjecutarAsync(CargarAsync);

    private async Task CargarAsync()
    {
        var panel = await JornadaService.Instancia.ObtenerPanelAsync();
        CantidadTexto = panel.Umbrales.Cantidad.ToString(CultureInfo.InvariantCulture);
        PesoTexto = panel.Umbrales.Peso.ToString(CultureInfo.InvariantCulture);
        Zonas = panel.Zonas.Select(z => new AvanceZona(z)).ToList();
        Jornadas = panel.Jornadas;
        SinZonaEspecifica = panel.SinZonaEspecifica;
        ErrorUmbrales = string.Empty;
    }

    [RelayCommand]
    private Task GuardarUmbrales() => EjecutarAsync(async () =>
    {
        AvisoUmbrales = string.Empty;
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
        AvisoUmbrales = "Umbrales guardados.";
    });

    partial void OnCantidadTextoChanged(string value) => LimpiarAvisos();
    partial void OnPesoTextoChanged(string value) => LimpiarAvisos();

    private void LimpiarAvisos()
    {
        ErrorUmbrales = string.Empty;
        AvisoUmbrales = string.Empty;
    }

    private async Task EjecutarAsync(Func<Task> operacion)
    {
        if (Ocupado)
            return;

        Ocupado = true;
        try { await OperacionBaseDatos.EjecutarAsync(operacion); }
        finally { Ocupado = false; }
    }

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

internal sealed record AvanceZona(ZonaDisponible Datos)
{
    public double AvanceCantidad => Proporcion(Datos.Cantidad, Datos.Umbrales.Cantidad);
    public double AvancePeso => Proporcion(Datos.Peso, Datos.Umbrales.Peso);
    public bool Lista => Datos.AlcanzaUmbral;
    public bool Proxima => !Lista && Math.Max(AvanceCantidad, AvancePeso) >= 0.75;
    public string Estado => Lista ? "Lista para generar" : Proxima ? "Próxima al umbral" : "Por debajo del umbral";
    public string Cantidad => $"{Datos.Cantidad} de {Datos.Umbrales.Cantidad} solicitudes";
    public string Peso => $"{Datos.Peso:0.##} de {Datos.Umbrales.Peso:0.##} kg";
    public string Accion => Lista ? "Revisar propuesta" : "Ver solicitudes";

    private static double Proporcion(double valor, double umbral) =>
        umbral > 0 ? Math.Clamp(valor / umbral, 0, 1) : 0;
}
