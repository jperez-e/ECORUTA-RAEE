using System.Linq;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

public partial class ResiduoElectronicoViewModel : ObservableObject
{
    private int residuoId;

    // Propiedades del modelo
    [ObservableProperty]
    private int solicitudId;

    [ObservableProperty]
    private string categoria = string.Empty;

    [ObservableProperty]
    private string descripcion = string.Empty;

    [ObservableProperty]
    private int cantidad = 1;

    [ObservableProperty]
    private double pesoEstimado;

    // Campos de texto para validar entradas numéricas
    [ObservableProperty]
    private string cantidadTexto = "1";

    [ObservableProperty]
    private string pesoTexto = string.Empty;

    // Solicitud seleccionada
    [ObservableProperty]
    private SolicitudRecoleccion? solicitudSeleccionada;

    // Modo del formulario
    [ObservableProperty]
    private bool esEdicion;

    // Mensajes de validación
    [ObservableProperty]
    private string errorSolicitud = string.Empty;

    [ObservableProperty]
    private string errorCategoria = string.Empty;

    [ObservableProperty]
    private string errorDescripcion = string.Empty;

    [ObservableProperty]
    private string errorCantidad = string.Empty;

    [ObservableProperty]
    private string errorPeso = string.Empty;

    // Colecciones para los Picker
    public ObservableCollection<SolicitudRecoleccion>
        SolicitudesDisponibles
    { get; } = new();

    public ObservableCollection<string> Categorias { get; } = new()
    {
        "Computadora",
        "Monitor",
        "Celular",
        "Impresora",
        "Tablet",
        "Televisor",
        "Otro"
    };

    // Propiedades dinámicas del formulario
    public string TituloFormulario =>
        EsEdicion ? "Editar residuo" : "Nuevo residuo";

    public string TextoBotonGuardar =>
        EsEdicion ? "Guardar cambios" : "Registrar residuo";

    public bool MostrarBotonEliminar => EsEdicion;

    // Visibilidad de errores
    public bool TieneErrorSolicitud =>
        !string.IsNullOrWhiteSpace(ErrorSolicitud);

    public bool TieneErrorCategoria =>
        !string.IsNullOrWhiteSpace(ErrorCategoria);

    public bool TieneErrorDescripcion =>
        !string.IsNullOrWhiteSpace(ErrorDescripcion);

    public bool TieneErrorCantidad =>
        !string.IsNullOrWhiteSpace(ErrorCantidad);

    public bool TieneErrorPeso =>
        !string.IsNullOrWhiteSpace(ErrorPeso);

    public ResiduoElectronicoViewModel()
    {
        CargarSolicitudesDisponibles();
    }

    // Actualizar título y botones al cambiar de modo
    partial void OnEsEdicionChanged(bool value)
    {
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(TextoBotonGuardar));
        OnPropertyChanged(nameof(MostrarBotonEliminar));
    }

    // Actualizar visibilidad de los errores
    partial void OnErrorSolicitudChanged(string value)
    {
        OnPropertyChanged(nameof(TieneErrorSolicitud));
    }

    partial void OnErrorCategoriaChanged(string value)
    {
        OnPropertyChanged(nameof(TieneErrorCategoria));
    }

    partial void OnErrorDescripcionChanged(string value)
    {
        OnPropertyChanged(nameof(TieneErrorDescripcion));
    }

    partial void OnErrorCantidadChanged(string value)
    {
        OnPropertyChanged(nameof(TieneErrorCantidad));
    }

    partial void OnErrorPesoChanged(string value)
    {
        OnPropertyChanged(nameof(TieneErrorPeso));
    }

    // Cargar solicitudes existentes
    public void CargarSolicitudesDisponibles()
    {
        SolicitudesDisponibles.Clear();

        var solicitudes =
            SolicitudService.Instancia.ObtenerTodas();

        foreach (var solicitud in solicitudes)
        {
            SolicitudesDisponibles.Add(solicitud);
        }
    }

    // Preparar el formulario para un registro nuevo
    public void PrepararNuevo()
    {
        residuoId = 0;

        SolicitudId = 0;
        SolicitudSeleccionada = null;

        Categoria = string.Empty;
        Descripcion = string.Empty;

        Cantidad = 1;
        PesoEstimado = 0;

        CantidadTexto = "1";
        PesoTexto = string.Empty;

        EsEdicion = false;

        LimpiarErrores();
        CargarSolicitudesDisponibles();
    }

    // Cargar los datos de un residuo para editar
    public void CargarParaEditar(ResiduoElectronico residuo)
    {
        if (residuo == null)
            return;

        CargarSolicitudesDisponibles();

        residuoId = residuo.Id;

        SolicitudId = residuo.SolicitudId;

        SolicitudSeleccionada =
            SolicitudesDisponibles.FirstOrDefault(
                s => s.Id == residuo.SolicitudId);

        Categoria = residuo.Categoria;
        Descripcion = residuo.Descripcion;

        Cantidad = residuo.Cantidad;
        PesoEstimado = residuo.PesoEstimado;

        CantidadTexto =
            residuo.Cantidad.ToString(
                CultureInfo.InvariantCulture);

        PesoTexto =
            residuo.PesoEstimado.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

        EsEdicion = true;

        LimpiarErrores();
    }

    // Validar los campos del formulario
    private bool Validar()
    {
        LimpiarErrores();

        bool valido = true;

        // Validar solicitud
        if (SolicitudSeleccionada == null ||
            SolicitudService.Instancia.ObtenerPorId(
                SolicitudSeleccionada.Id) == null)
        {
            ErrorSolicitud =
                "Debes seleccionar una solicitud válida.";

            valido = false;
        }

        // Validar categoría
        if (string.IsNullOrWhiteSpace(Categoria))
        {
            ErrorCategoria =
                "Selecciona una categoría.";

            valido = false;
        }

        // Validar descripción
        string descripcionLimpia =
            (Descripcion ?? string.Empty).Trim();

        if (descripcionLimpia.Length < 5 ||
            descripcionLimpia.Length > 150)
        {
            ErrorDescripcion =
                "La descripción debe tener entre 5 y 150 caracteres.";

            valido = false;
        }

        // Validar cantidad
        bool cantidadValida = int.TryParse(
            CantidadTexto?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int cantidadConvertida);

        if (!cantidadValida ||
            cantidadConvertida < 1 ||
            cantidadConvertida > 1000)
        {
            ErrorCantidad =
                "Introduce una cantidad entre 1 y 1000.";

            valido = false;
        }

        // Validar peso
        string pesoNormalizado =
            (PesoTexto ?? string.Empty)
                .Trim()
                .Replace(',', '.');

        bool pesoValido = double.TryParse(
            pesoNormalizado,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double pesoConvertido);

        if (!pesoValido ||
            double.IsNaN(pesoConvertido) ||
            double.IsInfinity(pesoConvertido) ||
            pesoConvertido <= 0 ||
            pesoConvertido > 10000)
        {
            ErrorPeso =
                "Introduce un peso mayor que 0 y hasta 10000 kg.";

            valido = false;
        }

        if (!valido)
            return false;

        // Actualizar las propiedades del modelo
        SolicitudId = SolicitudSeleccionada!.Id;
        Categoria = Categoria.Trim();
        Descripcion = descripcionLimpia;

        Cantidad = cantidadConvertida;
        PesoEstimado = pesoConvertido;

        return true;
    }

    // Crear el modelo a partir del formulario
    public ResiduoElectronico CrearModelo()
    {
        return new ResiduoElectronico
        {
            Id = residuoId,
            SolicitudId = SolicitudId,
            Categoria = Categoria,
            Descripcion = Descripcion,
            Cantidad = Cantidad,
            PesoEstimado = PesoEstimado
        };
    }

    // CREATE / UPDATE
    [RelayCommand]
    private async Task Guardar()
    {
        if (!Validar())
            return;

        var residuo = CrearModelo();

        bool resultado;

        if (EsEdicion)
        {
            resultado =
                ResiduoElectronicoService.Instancia
                    .Actualizar(residuo);
        }
        else
        {
            resultado =
                ResiduoElectronicoService.Instancia
                    .Crear(residuo);
        }

        if (!resultado)
        {
            await Shell.Current.DisplayAlertAsync(
                "Error",
                "No se pudo guardar el residuo electrónico.",
                "Aceptar");

            return;
        }

        string mensaje = EsEdicion
            ? "El residuo se actualizó correctamente."
            : "El residuo se registró correctamente.";

        await Shell.Current.DisplayAlertAsync(
            "Operación exitosa",
            mensaje,
            "Aceptar");

        await Shell.Current.GoToAsync("..");
    }

    // DELETE
    [RelayCommand]
    private async Task Eliminar()
    {
        if (!EsEdicion || residuoId <= 0)
            return;

        bool confirmar =
            await Shell.Current.DisplayAlertAsync(
                "Eliminar residuo",
                "¿Deseas eliminar este residuo electrónico?",
                "Sí, eliminar",
                "Cancelar");

        if (!confirmar)
            return;

        bool eliminado =
            ResiduoElectronicoService.Instancia
                .Eliminar(residuoId);

        if (!eliminado)
        {
            await Shell.Current.DisplayAlertAsync(
                "Error",
                "No se pudo eliminar el residuo.",
                "Aceptar");

            return;
        }

        await Shell.Current.DisplayAlertAsync(
            "Residuo eliminado",
            "El residuo se eliminó correctamente.",
            "Aceptar");

        await Shell.Current.GoToAsync("..");
    }

    // Cancelar y regresar
    [RelayCommand]
    private async Task Cancelar()
    {
        await Shell.Current.GoToAsync("..");
    }

    // Limpiar mensajes de error
    private void LimpiarErrores()
    {
        ErrorSolicitud = string.Empty;
        ErrorCategoria = string.Empty;
        ErrorDescripcion = string.Empty;
        ErrorCantidad = string.Empty;
        ErrorPeso = string.Empty;
    }

    // Preparar nuevo residuo asociado a una solicitud
    public void PrepararNuevoParaSolicitud(int solicitudId)
    {
        // Limpiar el formulario y cargar solicitudes
        PrepararNuevo();

        // Buscar la solicitud correspondiente
        var solicitud = SolicitudesDisponibles
            .FirstOrDefault(s => s.Id == solicitudId);

        if (solicitud == null)
            return;

        // Seleccionar automáticamente la solicitud
        SolicitudSeleccionada = solicitud;
        SolicitudId = solicitud.Id;
    }

}
