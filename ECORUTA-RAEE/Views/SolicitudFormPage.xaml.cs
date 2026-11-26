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

    bool IFormularioDemostracion.Ocupado => _viewModel.Guardando || _viewModel.EliminarCommand.IsRunning;
    bool IFormularioDemostracion.HayBorrador => _original is not null && _original != Capturar();
    bool IFormularioDemostracion.SalidaConfirmada => _viewModel.SalidaConfirmada;

    public SolicitudFormPage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new SolicitudFormViewModel();
        _viewModel.VolverAPortada = SesionDemostracion.Perfil == PerfilDemostracion.Ciudadano;
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
            await Navigation.PushModalAsync(new SelectorUbicacionPage(
                _viewModel.ObtenerPuntoRecogida(), _viewModel.AplicarPuntoRecogida));
        }
        catch (InvalidOperationException)
        {
            await DisplayAlertAsync("Mapa no disponible", "Puedes continuar con la dirección de referencia.", "Aceptar");
        }
        finally
        {
            _abriendoMapa = false;
        }
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
        _viewModel.DireccionReferencia, _viewModel.LatitudTexto, _viewModel.LongitudTexto,
        _viewModel.PersonalizarCodigo, _viewModel.CodigoTexto, _viewModel.PesoTexto,
        Equipos = _viewModel.Equipos.Select(e => new { e.IdPersistido, e.Categoria, e.Descripcion, e.CantidadTexto, e.PesoTexto })
    });
}
