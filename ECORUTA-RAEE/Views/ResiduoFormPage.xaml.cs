
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class ResiduoFormPage :
    ContentPage, IQueryAttributable
{
    private readonly ResiduoElectronicoViewModel _viewModel;

    public ResiduoFormPage()
    {
        InitializeComponent();

        _viewModel = new ResiduoElectronicoViewModel();

        BindingContext = _viewModel;

        // Preparar formulario limpio
        _viewModel.PrepararNuevo();
    }

    public void ApplyQueryAttributes(
        IDictionary<string, object> query)
    {
        // CASO 1: Editar un residuo existente
        if (query.TryGetValue("Residuo", out var valor)
            && valor is ResiduoElectronico residuo)
        {
            _viewModel.CargarParaEditar(residuo);
        }

        // CASO 2: Crear un residuo desde una solicitud
        else if (
            query.TryGetValue("SolicitudId", out var idValor)
            && idValor is int solicitudId)
        {
            _viewModel.PrepararNuevoParaSolicitud(
                solicitudId);
        }

        // CASO 3: Crear un residuo desde el menú
        else
        {
            _viewModel.PrepararNuevo();
        }
    }
}
