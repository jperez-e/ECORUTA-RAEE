using System.Text.Json;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudFormPage : ContentPage, IQueryAttributable, IFormularioDemostracion
{
    private readonly SolicitudFormViewModel _viewModel;
    private int? _solicitudId;
    private bool _cargando;
    private bool _abriendoMapa;
    private string? _original;
    private bool _edicionDenegada;

    internal bool AbriendoMapa => _abriendoMapa;

    bool IFormularioDemostracion.Ocupado => _viewModel.Guardando || _viewModel.EliminarCommand.IsRunning;
    bool IFormularioDemostracion.HayBorrador => _original is not null && _original != Capturar();
    bool IFormularioDemostracion.SalidaConfirmada => _viewModel.SalidaConfirmada;

    public SolicitudFormPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new SolicitudFormViewModel
        {
            PermitirCoordenadasAvanzadas = SesionDemostracion.Perfil == PerfilDemostracion.Gestor
        };
        _viewModel.VolverAPortada = SesionDemostracion.Perfil == PerfilDemostracion.Ciudadano;
        if (_viewModel.VolverAPortada)
            _viewModel.MostrarConfirmacionCiudadana = fila =>
                Shell.Current.Navigation.PushModalAsync(new ConsultaCiudadanaPage(fila, confirmacion: true));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (SesionDemostracion.Perfil != PerfilDemostracion.Gestor &&
            (query.ContainsKey("SolicitudId") || query.ContainsKey("VolverAJornada")))
        {
            _edicionDenegada = true;
            return;
        }
        if (query.TryGetValue("SolicitudId", out var valor) && valor is int solicitudId)
            _solicitudId = solicitudId;
        if (SesionDemostracion.Perfil == PerfilDemostracion.Gestor &&
            query.TryGetValue("VolverAListadoGestion", out var volverListado) && volverListado is true)
            _viewModel.VolverAListadoGestion = true;
        if (query.TryGetValue("VolverAJornada", out var volver) && volver is true)
            _viewModel.VolverAJornada = true;
    }

    protected override bool OnBackButtonPressed() =>
        _viewModel.Guardando || base.OnBackButtonPressed();

    private async void AbrirMapa_Clicked(object? sender, EventArgs e)
    {
        if (_abriendoMapa || !_viewModel.FormularioDisponible ||
            _viewModel.GuardarCommand.IsRunning || _viewModel.EliminarCommand.IsRunning)
            return;

        _abriendoMapa = true;
        try
        {
            bool asignada = _solicitudId is int id && await SolicitudService.Instancia.TieneAsignacionActivaAsync(id);
            var vigente = asignada && _solicitudId is int asignadaId
                ? await SolicitudService.Instancia.ObtenerPorIdAsync(asignadaId) : null;
            await Navigation.PushModalAsync(new SelectorUbicacionPage(
                _viewModel.ObtenerPuntoRecogida(), vigente?.Zona ?? _viewModel.ZonaSeleccionada,
                vigente?.SectorOtraZona ?? _viewModel.SectorOtraZona, _viewModel.AplicarUbicacion,
                bloquearZona: asignada, requierePunto: asignada));
        }
        catch (Exception ex) when (ex is InvalidOperationException or SQLite.SQLiteException or IOException)
        {
            await DisplayAlertAsync("Mapa no disponible", "Puedes continuar con la dirección de referencia.", "Aceptar");
        }
        finally
        {
            _abriendoMapa = false;
        }
    }

    private async Task IrAAsync(VisualElement campo, bool enfocar = false)
    {
        await FormularioScroll.ScrollToAsync(campo, ScrollToPosition.Start, true);
        if (enfocar && campo is Entry or Editor)
            campo.Focus();
    }

    private async void CambiarContacto_Clicked(object? sender, EventArgs e) => await IrAAsync(CampoNombre, true);
    private async void CambiarUbicacion_Clicked(object? sender, EventArgs e) => await IrAAsync(CampoMapa);
    private async void CambiarEquipos_Clicked(object? sender, EventArgs e) => await IrAAsync(SeccionEquipos);

    private async void Guardar_Clicked(object? sender, EventArgs e)
    {
        var guardar = _viewModel.GuardarCommand;
        if (guardar.IsRunning || !guardar.CanExecute(null))
            return;

        await guardar.ExecuteAsync(null);
        if (_viewModel.SalidaConfirmada)
            return;

        VisualElement? campo =
            _viewModel.ErrorNombreContacto.Length > 0 ? CampoNombre :
            _viewModel.ErrorTelefonoContacto.Length > 0 ? CampoTelefono :
            _viewModel.TieneErrorLatitud ? CampoLatitud :
            _viewModel.TieneErrorLongitud ? CampoLongitud :
            _viewModel.TieneErrorZona ? _viewModel.EsOtraZona && string.IsNullOrWhiteSpace(_viewModel.SectorOtraZona)
                ? CampoSector : CampoZona :
            _viewModel.TieneErrorDireccion ? CampoReferencia :
            _viewModel.ErrorEquipos.Length > 0 ? BuscarErrorEquipo() :
            _viewModel.TieneErrorCodigo ? CampoCodigo :
            _viewModel.TieneErrorPeso ? CampoPesoManual : null;
        if (campo is not null)
            await IrAAsync(campo, true);
    }

    private VisualElement BuscarErrorEquipo()
    {
        var fila = _viewModel.Equipos.FirstOrDefault(e => e.ErrorCategoria.Length + e.ErrorDescripcion.Length +
            e.ErrorCantidad.Length + e.ErrorPeso.Length > 0);
        if (fila is null)
            return AgregarEquipo;
        string campo = fila.ErrorCategoria.Length > 0 ? "Categoría del equipo"
            : fila.ErrorDescripcion.Length > 0 ? "Descripción del equipo"
            : fila.ErrorCantidad.Length > 0 ? "Cantidad de equipos" : "Peso total del registro en kilogramos";
        return SeccionEquipos.GetVisualTreeDescendants().OfType<VisualElement>().FirstOrDefault(
            vista => ReferenceEquals(vista.BindingContext, fila) && SemanticProperties.GetDescription(vista) == campo)
            ?? SeccionEquipos;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_edicionDenegada || SesionDemostracion.Perfil == PerfilDemostracion.Recolector)
        {
            Content = TarjetasDemo.Texto("Este perfil no puede abrir este formulario.");
            return;
        }
        if (_cargando || _viewModel.FormularioDisponible)
            return;

        _cargando = true;
        try
        {
            await OperacionBaseDatos.EjecutarAsync(async () =>
            {
                if (_solicitudId is int id)
                {
                    _viewModel.FormularioDisponible = await _viewModel.CargarParaEditarAsync(id);
                }
                else
                {
                    _viewModel.PrepararNuevaSolicitud();
                    _viewModel.FormularioDisponible = true;
                }
                if (_viewModel.FormularioDisponible)
                    _original = Capturar();
            });
        }
        finally
        {
            _cargando = false;
        }
    }

    private string Capturar() => JsonSerializer.Serialize(new
    {
        _viewModel.ZonaSeleccionada, _viewModel.SectorOtraZona, _viewModel.ZonaConfirmada,
        _viewModel.NombreContacto, _viewModel.TelefonoContacto, _viewModel.PrefijoContacto,
        _viewModel.DireccionReferencia, _viewModel.LatitudTexto, _viewModel.LongitudTexto,
        _viewModel.PersonalizarCodigo, _viewModel.CodigoTexto, _viewModel.PesoTexto,
        Equipos = _viewModel.Equipos.Select(e => new { e.IdPersistido, e.Categoria, e.Descripcion, e.CantidadTexto, e.PesoTexto })
    });
}
