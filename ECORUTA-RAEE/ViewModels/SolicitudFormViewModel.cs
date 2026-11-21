
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using SQLite;

namespace ECORUTA_RAEE.ViewModels;

public partial class SolicitudFormViewModel : ObservableObject
{
    private int solicitudId;
    private string codigoSolicitud = string.Empty;

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
    private bool personalizarCodigo;

    [ObservableProperty]
    private string codigoTexto = string.Empty;

    [ObservableProperty]
    private string errorCodigo = string.Empty;

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

    [ObservableProperty]
    private bool esEdicion;

    // Indica que el peso procede de los residuos asociados.
    [ObservableProperty]
    private bool pesoEsCalculado;

    public bool PesoEditable => !PesoEsCalculado;

    // Propiedades dinámicas para la interfaz.
    public string TituloFormulario =>
        EsEdicion ? "Editar solicitud" : "Nueva solicitud";

    public string TextoBotonGuardar =>
        EsEdicion ? "Guardar cambios   →" : "Registrar solicitud   →";

    public bool MostrarBotonEliminar => EsEdicion;

    public bool TieneErrorZona => !string.IsNullOrEmpty(ErrorZona);
    public bool TieneErrorDireccion => !string.IsNullOrEmpty(ErrorDireccion);
    public bool TieneErrorPeso => !string.IsNullOrEmpty(ErrorPeso);
    public bool TieneErrorCodigo => !string.IsNullOrEmpty(ErrorCodigo);

    partial void OnEsEdicionChanged(bool value)
    {
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(TextoBotonGuardar));
        OnPropertyChanged(nameof(MostrarBotonEliminar));
    }

    partial void OnPesoEsCalculadoChanged(bool value)
    {
        OnPropertyChanged(nameof(PesoEditable));
    }

    partial void OnErrorZonaChanged(string value) =>
        OnPropertyChanged(nameof(TieneErrorZona));

    partial void OnErrorDireccionChanged(string value) =>
        OnPropertyChanged(nameof(TieneErrorDireccion));

    partial void OnErrorPesoChanged(string value) =>
        OnPropertyChanged(nameof(TieneErrorPeso));

    partial void OnErrorCodigoChanged(string value) =>
        OnPropertyChanged(nameof(TieneErrorCodigo));

    partial void OnCodigoTextoChanged(string value) => ErrorCodigo = string.Empty;

    partial void OnPersonalizarCodigoChanged(bool value) => ErrorCodigo = string.Empty;

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

    // Preparar el formulario para una nueva solicitud.
    // El peso debe estar habilitado para entrada manual.
    public void PrepararNuevaSolicitud()
    {
        solicitudId = 0;
        codigoSolicitud = string.Empty;
        PersonalizarCodigo = false;
        CodigoTexto = string.Empty;

        EsEdicion = false;
        PesoEsCalculado = false;

        ZonaSeleccionada = null;
        DireccionReferencia = string.Empty;
        PesoTexto = string.Empty;

        LimpiarErrores();
    }

    // Cargar los datos de una solicitud para editar.
    public async Task<bool> CargarParaEditarAsync(int id)
    {
        var solicitud = await SolicitudService.Instancia.ObtenerPorIdAsync(id);
        if (solicitud is null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible", "La solicitud ya no existe.", "Aceptar");
            return false;
        }

        PesoEsCalculado = await ResiduoElectronicoService.Instancia.ContarPorSolicitudIdAsync(id) > 0;
        solicitudId = solicitud.Id;
        codigoSolicitud = solicitud.CodigoSolicitud;
        PersonalizarCodigo = false;
        CodigoTexto = codigoSolicitud;

        ZonaSeleccionada = solicitud.Zona;
        DireccionReferencia = solicitud.DireccionReferencia;

        PesoTexto = solicitud.PesoTotalEstimado.ToString(CultureInfo.InvariantCulture);

        LimpiarErrores();
        EsEdicion = true;
        return true;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(EliminarCommand))]
    private bool formularioDisponible;


    [RelayCommand(CanExecute = nameof(FormularioDisponible))]
    private Task Guardar() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        LimpiarErrores();

        bool valido = true;

        int? numeroPersonalizado = null;
        bool conservaCodigo = EsEdicion && string.Equals(
            CodigoTexto?.Trim(), codigoSolicitud, StringComparison.OrdinalIgnoreCase);
        if (PersonalizarCodigo && !conservaCodigo)
        {
            if (SolicitudService.TryLeerNumeroCodigo(CodigoTexto, out int numero))
            {
                numeroPersonalizado = numero;
            }
            else
            {
                ErrorCodigo = string.IsNullOrWhiteSpace(CodigoTexto)
                    ? "Introduce un código de solicitud."
                    : "Usa el formato SOL-0001, con un número del 1 al 2 147 483 647.";
                valido = false;
            }
        }

        // Validar zona.
        if (string.IsNullOrWhiteSpace(ZonaSeleccionada))
        {
            ErrorZona = "Selecciona la zona de recogida.";
            valido = false;
        }

        // Validar dirección.
        string direccion = (DireccionReferencia ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(direccion))
        {
            ErrorDireccion = "Introduce la dirección o referencia de recogida.";
            valido = false;
        }
        else if (direccion.Length < 5)
        {
            ErrorDireccion = "La dirección debe contener al menos 5 caracteres.";
            valido = false;
        }
        else if (direccion.Length > 150)
        {
            ErrorDireccion = "La dirección no puede superar los 150 caracteres.";
            valido = false;
        }

        // Consultar el estado ACTUAL de los residuos al guardar.
        var servicioResiduos = ResiduoElectronicoService.Instancia;

        var solicitudActual = EsEdicion
            ? await SolicitudService.Instancia.ObtenerPorIdAsync(solicitudId)
            : null;

        if (EsEdicion && solicitudActual is null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible", "La solicitud ya no existe.", "Aceptar");
            return;
        }

        bool tieneResiduos = EsEdicion &&
            await servicioResiduos.ContarPorSolicitudIdAsync(solicitudId) > 0;

        bool conservarPesoRegistrado = tieneResiduos || PesoEsCalculado;
        PesoEsCalculado = tieneResiduos;

        double peso = 0;

        if (conservarPesoRegistrado)
        {
            // El peso se calcula, no se acepta un valor manual.
            peso = solicitudActual!.PesoTotalEstimado;

            PesoTexto = peso.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            // Si no hay residuos, conservar el peso manual.
            string pesoNormalizado = (PesoTexto ?? string.Empty)
                .Trim().Replace(',', '.');

            bool pesoValido = double.TryParse(
                pesoNormalizado,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out peso);

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
            else
            {
                // Se puede conservar 0 kg si el último residuo
                // se eliminó y la solicitud ya quedó en 0.
                bool conservaCeroCalculado =
                    EsEdicion &&
                    peso == 0 &&
                    solicitudActual?.PesoTotalEstimado == 0;

                if (peso <= 0 && !conservaCeroCalculado)
                {
                    ErrorPeso = "El peso debe ser mayor que 0 kg.";
                    valido = false;
                }
                else if (peso > 10000)
                {
                    ErrorPeso = "El peso supera el límite de 10,000 kg.";
                    valido = false;
                }
            }
        }

        if (!valido)
        {
            GuardadoExitoso = false;
            return;
        }

        var solicitud = new SolicitudRecoleccion
        {
            Id = solicitudId,
            CodigoSolicitud = solicitudActual?.CodigoSolicitud ?? string.Empty,
            Zona = ZonaSeleccionada!,
            DireccionReferencia = direccion,
            PesoTotalEstimado = peso
        };

        try
        {
            if (EsEdicion)
            {
                bool actualizado = await SolicitudService.Instancia.ActualizarAsync(
                    solicitud, actualizarPesoManual: !conservarPesoRegistrado,
                    numeroPersonalizado: numeroPersonalizado);

                if (!actualizado)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Solicitud no disponible",
                        "No se encontró la solicitud que deseas actualizar.",
                        "Aceptar");
                    return;
                }
            }
            else
            {
                await SolicitudService.Instancia.CrearAsync(solicitud, numeroPersonalizado);
            }
        }
        catch (CodigoSolicitudDuplicadoException) when (numeroPersonalizado.HasValue)
        {
            ErrorCodigo = "Ya existe una solicitud con este código. Introduce uno diferente.";
            return;
        }
        catch (CodigoSolicitudDuplicadoException ex)
        {
            throw new InvalidOperationException("La secuencia generó un código existente.", ex);
        }
        catch (SQLiteException ex) when (
            ((int)ex.Result & 0xff) == (int)SQLite3.Result.Constraint &&
            ex.Message == "La solicitud está asignada a una jornada activa.")
        {
            ErrorZona = "Cancela la jornada activa antes de cambiar la zona de esta solicitud.";
            return;
        }

        codigoSolicitud = solicitud.CodigoSolicitud;
        string accion = EsEdicion ? "actualizó" : "guardó";
        MensajeExito = $"La solicitud {codigoSolicitud} se {accion} correctamente.";
        GuardadoExitoso = !EsEdicion;

        await Shell.Current.DisplayAlertAsync(
            EsEdicion ? "Actualización exitosa" : "Registro exitoso",
            MensajeExito,
            "Aceptar");

        await Shell.Current.GoToAsync("//solicitudes");
    });

    // Eliminar desde el formulario.
    [RelayCommand(CanExecute = nameof(FormularioDisponible))]
    private Task Eliminar() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        if (!EsEdicion)
            return;

        int cantidadResiduos = await ResiduoElectronicoService.Instancia
            .ContarPorSolicitudIdAsync(solicitudId);

        string mensaje = cantidadResiduos > 0
            ? $"¿Estás seguro de que deseas eliminar la solicitud {codigoSolicitud} y sus {cantidadResiduos} residuos asociados? Esta acción no se puede deshacer."
            : $"¿Estás seguro de que deseas eliminar la solicitud {codigoSolicitud}?";

        if (await JornadaService.Instancia.TieneJornadasAsync(solicitudId))
            mensaje += "\n\nTambién se quitará de sus jornadas. Si era la última solicitud de una jornada activa, esa jornada se cancelará.";

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Eliminar solicitud",
            mensaje,
            "Sí, eliminar",
            "Cancelar");

        if (!confirmar)
            return;

        bool eliminado = await SolicitudService.Instancia.EliminarAsync(solicitudId);

        if (!eliminado)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible",
                "La solicitud ya no existe.",
                "Aceptar");

            return;
        }

        await Shell.Current.DisplayAlertAsync(
            "Solicitud eliminada",
            "La solicitud y sus residuos asociados se eliminaron correctamente.",
            "Aceptar");

        await Shell.Current.GoToAsync("//solicitudes");
    });

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
        ErrorCodigo = string.Empty;

        GuardadoExitoso = false;
        MensajeExito = string.Empty;
    }
}
