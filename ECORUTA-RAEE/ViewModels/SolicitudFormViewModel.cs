using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class SolicitudFormViewModel : ObservableObject
{
    public ObservableCollection<string> Zonas { get; } = new()
    {
        "Herrera",
        "Los Alcarrizos",
        "Manoguayabo",
        "Pantoja",
        "Pedro Brand",
        "Otra zona"
    };

    [ObservableProperty]
    private string? zonaSeleccionada;

    [ObservableProperty]
    private string direccionReferencia = string.Empty;

    [ObservableProperty]
    private string pesoTexto = string.Empty;

    [ObservableProperty]
    private string errorZona = string.Empty;

    [ObservableProperty]
    private string errorDireccion = string.Empty;

    [ObservableProperty]
    private string errorPeso = string.Empty;

    [ObservableProperty]
    private bool guardadoExitoso;

    [ObservableProperty]
    private string mensajeExito = string.Empty;

    public bool TieneErrorZona => !string.IsNullOrEmpty(ErrorZona);

    public bool TieneErrorDireccion =>
        !string.IsNullOrEmpty(ErrorDireccion);

    public bool TieneErrorPeso => !string.IsNullOrEmpty(ErrorPeso);

    partial void OnErrorZonaChanged(string value)
        => OnPropertyChanged(nameof(TieneErrorZona));

    partial void OnErrorDireccionChanged(string value)
        => OnPropertyChanged(nameof(TieneErrorDireccion));

    partial void OnErrorPesoChanged(string value)
        => OnPropertyChanged(nameof(TieneErrorPeso));

    partial void OnZonaSeleccionadaChanged(string? value)
    {
        ErrorZona = string.Empty;
        GuardadoExitoso = false;
    }

    partial void OnDireccionReferenciaChanged(string value)
    {
        ErrorDireccion = string.Empty;
        GuardadoExitoso = false;
    }

    partial void OnPesoTextoChanged(string value)
    {
        ErrorPeso = string.Empty;
        GuardadoExitoso = false;
    }

    [RelayCommand]
    private async Task Guardar()
    {
        LimpiarErrores();

        bool valido = true;

        // Validación de la zona.
        if (string.IsNullOrWhiteSpace(ZonaSeleccionada))
        {
            ErrorZona = "Selecciona la zona de recogida.";
            valido = false;
        }

        // Validación de la dirección.
        string direccion = DireccionReferencia.Trim();

        if (string.IsNullOrWhiteSpace(direccion))
        {
            ErrorDireccion =
                "Introduce la dirección o referencia de recogida.";
            valido = false;
        }
        else if (direccion.Length < 5)
        {
            ErrorDireccion =
                "La dirección debe contener al menos 5 caracteres.";
            valido = false;
        }
        else if (direccion.Length > 150)
        {
            ErrorDireccion =
                "La dirección no puede superar los 150 caracteres.";
            valido = false;
        }

        // Validación del peso.
        string pesoNormalizado = PesoTexto.Trim().Replace(',', '.');

        bool pesoValido = double.TryParse(
            pesoNormalizado,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out double peso);

        if (string.IsNullOrWhiteSpace(PesoTexto))
        {
            ErrorPeso = "Introduce el peso estimado en kilogramos.";
            valido = false;
        }
        else if (!pesoValido ||
                 double.IsNaN(peso) ||
                 double.IsInfinity(peso))
        {
            ErrorPeso = "Introduce un peso numérico válido.";
            valido = false;
        }
        else if (peso <= 0)
        {
            ErrorPeso = "El peso debe ser mayor que 0 kg.";
            valido = false;
        }
        else if (peso > 10000)
        {
            ErrorPeso = "El peso supera el límite de 10,000 kg.";
            valido = false;
        }

        if (!valido)
        {
            GuardadoExitoso = false;
            return;
        }

        var solicitud = new SolicitudRecoleccion
        {
            Zona = ZonaSeleccionada!,
            DireccionReferencia = direccion,
            PesoTotalEstimado = peso,
            Estado = "Pendiente"
        };

        SolicitudService.Instancia.Crear(solicitud);

        MensajeExito =
            $"La solicitud {solicitud.CodigoSolicitud} se guardó correctamente.";

        GuardadoExitoso = true;

        // Limpiar los campos después de guardar.
        ZonaSeleccionada = null;
        DireccionReferencia = string.Empty;
        PesoTexto = string.Empty;

        // Actualizar el listado.
        SolicitudesViewModel? listado = null;

        // Mostrar la confirmación solicitada por el profesor.
        await Shell.Current.DisplayAlert(
            "Registro exitoso",
            MensajeExito,
            "Aceptar");

        await Shell.Current.GoToAsync("//solicitudes");
    }

    [RelayCommand]
    private async Task Cancelar()
    {
        await Shell.Current.GoToAsync("..");
    }

    private void LimpiarErrores()
    {
        ErrorZona = string.Empty;
        ErrorDireccion = string.Empty;
        ErrorPeso = string.Empty;
        GuardadoExitoso = false;
        MensajeExito = string.Empty;
    }
}
