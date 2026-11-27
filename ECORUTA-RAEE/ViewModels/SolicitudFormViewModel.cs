
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
    private bool altaConfirmada;
    private SolicitudRecoleccion? solicitudOriginal;
    internal bool VolverAJornada { get; set; }
    internal bool VolverAPortada { get; set; }
    internal bool SalidaConfirmada { get; private set; }

    public ObservableCollection<string> Zonas { get; } = new(ElegibilidadRecogida.Zonas);
    public bool PermitirCoordenadasAvanzadas { get; init; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsOtraZona), nameof(PuedeConfirmarZona))]
    private string? zonaSeleccionada;

    [ObservableProperty]
    private string sectorOtraZona = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoZona))]
    private bool zonaConfirmada;

    [ObservableProperty]
    private string direccionReferencia = string.Empty;

    [ObservableProperty]
    private string pesoTexto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneUbicacion), nameof(EstadoUbicacion))]
    private string latitudTexto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneUbicacion), nameof(EstadoUbicacion))]
    private string longitudTexto = string.Empty;

    [ObservableProperty]
    private bool mostrarCoordenadasAvanzadas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneErrorLatitud))]
    private string errorLatitud = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneErrorLongitud))]
    private string errorLongitud = string.Empty;

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

    public bool PesoEditable => EsEdicion && SinEquipos && _equiposOriginales.Count == 0;

    // Propiedades dinámicas para la interfaz.
    public string TituloFormulario =>
        EsEdicion ? "Editar solicitud" : "Nueva solicitud";

    public string TextoBotonGuardar =>
        EsEdicion ? "Guardar cambios   →" : "Registrar solicitud   →";

    public bool MostrarBotonEliminar => EsEdicion;

    public IReadOnlyList<string> PrefijosContacto { get; } = [DatosContacto.Internacional, DatosContacto.Dominicano];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ResumenContacto))]
    private string nombreContacto = string.Empty;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ResumenContacto))]
    private string telefonoContacto = string.Empty;

    [ObservableProperty]
    private string prefijoContacto = DatosContacto.Internacional;

    [ObservableProperty] private string errorNombreContacto = string.Empty;
    [ObservableProperty] private string errorTelefonoContacto = string.Empty;

    public string ResumenContacto => string.Join(" · ", new[]
    {
        DatosContacto.NormalizarNombre(NombreContacto) ?? "Sin nombre registrado",
        DatosContacto.TryNormalizarTelefono(TelefonoContacto, PrefijoContacto == DatosContacto.Dominicano, out var tel)
            ? tel ?? "Sin teléfono registrado" : "Revisa el teléfono"
    });

    partial void OnNombreContactoChanged(string value) => ErrorNombreContacto = string.Empty;
    partial void OnTelefonoContactoChanged(string value) => ErrorTelefonoContacto = string.Empty;
    partial void OnPrefijoContactoChanged(string value)
    {
        ErrorTelefonoContacto = string.Empty;
        OnPropertyChanged(nameof(ResumenContacto));
    }

    public bool TieneErrorZona => !string.IsNullOrEmpty(ErrorZona);
    public bool TieneErrorDireccion => !string.IsNullOrEmpty(ErrorDireccion);
    public bool TieneErrorPeso => !string.IsNullOrEmpty(ErrorPeso);
    public bool TieneErrorCodigo => !string.IsNullOrEmpty(ErrorCodigo);
    public bool TieneErrorLatitud => ErrorLatitud.Length > 0;
    public bool TieneErrorLongitud => ErrorLongitud.Length > 0;
    public bool TieneUbicacion => ObtenerPuntoRecogida() is not null;
    public bool EsOtraZona => ZonaSeleccionada?.Trim().Equals("Otra zona", StringComparison.OrdinalIgnoreCase) == true;
    public bool PuedeConfirmarZona => !string.IsNullOrWhiteSpace(ZonaSeleccionada);
    public string EstadoZona => ZonaConfirmada
        ? $"Confirmaste la zona {ZonaSeleccionada}. Sin verificación geográfica."
        : "Confirma que la zona corresponde al punto o a la dirección.";
    public string TextoBotonMapa => TieneUbicacion ? "Revisar o cambiar el punto" : "Elegir en el mapa";
    public string EstadoUbicacion => TieneUbicacion
        ? "Ubicación seleccionada. Se guardará con la solicitud."
        : "Ubicación por completar. Puedes registrar la solicitud con su referencia. No se incluirá en una jornada automática hasta completar la ubicación.";

    internal PuntoRecogida? ObtenerPuntoRecogida() =>
        TryLeerCoordenada(LatitudTexto, Coordenadas.LimiteLatitud, out double latitud) &&
        TryLeerCoordenada(LongitudTexto, Coordenadas.LimiteLongitud, out double longitud)
            ? PuntoRecogida.Crear(latitud, longitud) : null;

    internal void AplicarPuntoRecogida(PuntoRecogida punto)
    {
        LatitudTexto = punto.Latitud.ToString("R", CultureInfo.InvariantCulture);
        LongitudTexto = punto.Longitud.ToString("R", CultureInfo.InvariantCulture);
    }

    internal void AplicarUbicacion(SeleccionUbicacion seleccion)
    {
        if (seleccion.Punto is { } punto)
            AplicarPuntoRecogida(punto);
        else
            QuitarUbicacion();

        ZonaSeleccionada = seleccion.Zona;
        SectorOtraZona = seleccion.Sector ?? string.Empty;
        ZonaConfirmada = true;
    }

    [RelayCommand]
    private void QuitarUbicacion()
    {
        LatitudTexto = string.Empty;
        LongitudTexto = string.Empty;
    }

    partial void OnLatitudTextoChanged(string value) => ActualizarConfirmacionUbicacion();
    partial void OnLongitudTextoChanged(string value) => ActualizarConfirmacionUbicacion();

    private void ActualizarConfirmacionUbicacion()
    {
        LimpiarErroresCoordenadas();
        ZonaConfirmada = false;
        OnPropertyChanged(nameof(TextoBotonMapa));
    }

    partial void OnSectorOtraZonaChanged(string value)
    {
        if (!EsOtraZona)
            return;

        ZonaConfirmada = false;
        ErrorZona = string.Empty;
    }

    partial void OnZonaConfirmadaChanged(bool value)
    {
        OnPropertyChanged(nameof(ZonaResumen));
        if (value)
            ErrorZona = string.Empty;
    }

    partial void OnEsEdicionChanged(bool value)
    {
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(TextoBotonGuardar));
        OnPropertyChanged(nameof(MostrarBotonEliminar));
        OnPropertyChanged(nameof(PesoEditable));
        OnPropertyChanged(nameof(PesoResumen));
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
        ZonaConfirmada = false;
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
        OnPropertyChanged(nameof(PesoResumen));
        GuardadoExitoso = false;
    }

    // Preparar el formulario para una nueva solicitud.
    public void PrepararNuevaSolicitud()
    {
        SalidaConfirmada = false;
        solicitudId = 0;
        altaConfirmada = false;
        solicitudOriginal = null;
        codigoSolicitud = string.Empty;
        PersonalizarCodigo = false;
        CodigoTexto = string.Empty;

        EsEdicion = false;
        CargarEquipos(Array.Empty<ResiduoElectronico>(), 0);

        NombreContacto = TelefonoContacto = string.Empty;
        PrefijoContacto = DatosContacto.Internacional;
        ZonaSeleccionada = null;
        SectorOtraZona = string.Empty;
        ZonaConfirmada = false;
        DireccionReferencia = string.Empty;
        PesoTexto = string.Empty;
        LatitudTexto = string.Empty;
        LongitudTexto = string.Empty;

        LimpiarErrores();
    }

    // Cargar los datos de una solicitud para editar.
    public async Task<bool> CargarParaEditarAsync(int id)
    {
        SalidaConfirmada = false;
        var (solicitud, equipos) = await SolicitudService.Instancia.ObtenerCompletaAsync(id);
        if (solicitud is null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Solicitud no disponible", "La solicitud ya no existe.", "Aceptar");
            return false;
        }

        CargarEquipos(equipos, solicitud.PesoTotalEstimado);
        solicitudId = solicitud.Id;
        solicitudOriginal = solicitud;
        codigoSolicitud = solicitud.CodigoSolicitud;
        PersonalizarCodigo = false;
        CodigoTexto = codigoSolicitud;

        NombreContacto = solicitud.NombreContacto ?? string.Empty;
        bool dominicano = solicitud.TelefonoContacto is { } tel &&
            (tel.StartsWith("+1809") || tel.StartsWith("+1829") || tel.StartsWith("+1849"));
        PrefijoContacto = dominicano ? DatosContacto.Dominicano : DatosContacto.Internacional;
        TelefonoContacto = dominicano ? solicitud.TelefonoContacto![2..] : solicitud.TelefonoContacto ?? string.Empty;
        ZonaSeleccionada = solicitud.Zona;
        SectorOtraZona = solicitud.SectorOtraZona ?? string.Empty;
        ZonaConfirmada = false;
        DireccionReferencia = solicitud.DireccionReferencia;

        PesoTexto = solicitud.PesoTotalEstimado.ToString(CultureInfo.InvariantCulture);
        LatitudTexto = solicitud.Latitud?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
        LongitudTexto = solicitud.Longitud?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;

        LimpiarErrores();
        EsEdicion = true;
        return true;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(EliminarCommand))]
    [NotifyPropertyChangedFor(nameof(PuedeEditarFormulario))]
    private bool formularioDisponible;


    [RelayCommand(CanExecute = nameof(FormularioDisponible))]
    private async Task Guardar()
    {
        if (!PuedeEditarFormulario || altaConfirmada || EliminarCommand.IsRunning)
            return;

        Guardando = true;
        try { await OperacionBaseDatos.EjecutarAsync(GuardarBorradorAsync); }
        finally { Guardando = false; }
    }

    private async Task GuardarBorradorAsync()
    {
        LimpiarErrores();

        bool valido = ValidarCoordenadas(out double? latitud, out double? longitud);
        if (!valido)
            MostrarCoordenadasAvanzadas = true;

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
        string? sector = EsOtraZona ? SectorOtraZona.Trim() : null;
        if (!PuedeConfirmarZona)
        {
            ErrorZona = "Selecciona la zona de recogida.";
            valido = false;
        }
        else if (EsOtraZona && (string.IsNullOrWhiteSpace(sector) || sector.Length > 100))
        {
            ErrorZona = "Escribe el sector o comunidad (hasta 100 caracteres).";
            valido = false;
        }
        else if (!ZonaConfirmada)
        {
            ErrorZona = "Confirma la zona de recogida antes de guardar.";
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

        ErrorEquipos = string.Empty;
        var borrador = new List<ResiduoElectronico>(_equipos.Count);
        foreach (var fila in _equipos)
        {
            if (fila.Validar() is { } equipo)
                borrador.Add(equipo);
            else
                valido = false;
        }

        if (!EsEdicion && SinEquipos)
        {
            ErrorEquipos = "Agrega al menos un equipo antes de guardar.";
            valido = false;
        }
        else if (borrador.Count != _equipos.Count)
        {
            ErrorEquipos = "Revisa los campos indicados en los equipos.";
        }

        double peso = borrador.Sum(e => e.PesoEstimado);
        if (PesoEditable)
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
                    _pesoOriginal == 0;

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

        var nombre = DatosContacto.NormalizarNombre(NombreContacto);
        if (!DatosContacto.NombreValido(nombre))
            ErrorNombreContacto = "Escribe un nombre de hasta 100 caracteres.";
        if (!DatosContacto.TryNormalizarTelefono(TelefonoContacto, PrefijoContacto == DatosContacto.Dominicano, out var telefono))
            ErrorTelefonoContacto = PrefijoContacto == DatosContacto.Dominicano
                ? "Introduce los 10 dígitos, comenzando por 809, 829 o 849."
                : "Introduce el número completo con + y su código de país.";
        if (ErrorNombreContacto.Length > 0 || ErrorTelefonoContacto.Length > 0)
            return;

        var solicitud = new SolicitudRecoleccion
        {
            Id = solicitudId,
            CodigoSolicitud = codigoSolicitud,
            Zona = ZonaSeleccionada!,
            SectorOtraZona = sector,
            NombreContacto = nombre, TelefonoContacto = telefono,
            DireccionReferencia = direccion,
            Latitud = latitud,
            Longitud = longitud,
            PesoTotalEstimado = peso
        };

        try
        {
            if (!await SolicitudService.Instancia.GuardarSolicitudCompletaAsync(
                solicitud, borrador, _equiposOriginales, solicitudOriginal, numeroPersonalizado))
            {
                await Shell.Current.DisplayAlertAsync(
                    "Solicitud no disponible", "La solicitud ya no existe.", "Aceptar");
                return;
            }
        }
        catch (EquiposSolicitudModificadosException)
        {
            ErrorEquipos = "Los equipos cambiaron en otra pantalla. Vuelve a abrir la solicitud antes de guardar.";
            return;
        }
        catch (UbicacionAsignadaException ex)
        {
            await Shell.Current.DisplayAlertAsync("Ubicación asignada", ex.Message, "Aceptar");
            return;
        }
        catch (SolicitudModificadaException)
        {
            ErrorEquipos = "La solicitud cambió en otra pantalla. Vuelve a abrirla antes de guardar.";
            return;
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
        altaConfirmada = !EsEdicion;
        string accion = EsEdicion ? "actualizó" : "guardó";
        MensajeExito = $"La solicitud {codigoSolicitud} se {accion} correctamente.";
        GuardadoExitoso = !EsEdicion;

        await Shell.Current.DisplayAlertAsync(
            EsEdicion ? "Actualización exitosa" : "Registro exitoso",
            MensajeExito,
            "Aceptar");

        SalidaConfirmada = true;
        await VolverDespuesDeGuardarAsync();
    }

    // Eliminar desde el formulario.
    [RelayCommand(CanExecute = nameof(FormularioDisponible))]
    private Task Eliminar() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        if (!EsEdicion || Guardando)
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

        SalidaConfirmada = true;
        await VolverDespuesDeGuardarAsync();
    });

    [RelayCommand]
    private async Task Cancelar()
    {
        if (!Guardando)
            await Shell.Current.GoToAsync("..");
    }

    private Task VolverDespuesDeGuardarAsync() =>
        Shell.Current.GoToAsync(VolverAJornada || VolverAPortada ? ".." : "//solicitudes");

    private bool ValidarCoordenadas(out double? latitud, out double? longitud)
    {
        latitud = null;
        longitud = null;
        if (string.IsNullOrWhiteSpace(LatitudTexto) && string.IsNullOrWhiteSpace(LongitudTexto))
            return true;

        if (TryLeerCoordenada(LatitudTexto, Coordenadas.LimiteLatitud, out double valorLatitud))
            latitud = valorLatitud;
        else
            ErrorLatitud = string.IsNullOrWhiteSpace(LatitudTexto)
                ? "Introduce la latitud para completar las coordenadas."
                : "Introduce una latitud entre -90 y 90.";

        if (TryLeerCoordenada(LongitudTexto, Coordenadas.LimiteLongitud, out double valorLongitud))
            longitud = valorLongitud;
        else
            ErrorLongitud = string.IsNullOrWhiteSpace(LongitudTexto)
                ? "Introduce la longitud para completar las coordenadas."
                : "Introduce una longitud entre -180 y 180.";

        return latitud.HasValue && longitud.HasValue;
    }

    private static bool TryLeerCoordenada(string? texto, double limite, out double valor) =>
        double.TryParse(texto?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor)
        && Coordenadas.EnRango(valor, limite);

    private void LimpiarErroresCoordenadas()
    {
        ErrorLatitud = string.Empty;
        ErrorLongitud = string.Empty;
    }

    private void LimpiarErrores()
    {
        ErrorNombreContacto = ErrorTelefonoContacto = string.Empty;
        ErrorZona = string.Empty;
        ErrorDireccion = string.Empty;
        ErrorPeso = string.Empty;
        ErrorCodigo = string.Empty;
        LimpiarErroresCoordenadas();

        GuardadoExitoso = false;
        MensajeExito = string.Empty;
    }
}
