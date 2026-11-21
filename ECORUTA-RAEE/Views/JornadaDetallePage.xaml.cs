using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class JornadaDetallePage : ContentPage, IQueryAttributable
{
    private readonly JornadaRecoleccionViewModel _viewModel;

    public JornadaDetallePage()
    {
        InitializeComponent();
        BindingContext = _viewModel = new JornadaRecoleccionViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        query.TryGetValue("Zona", out var zona);
        query.TryGetValue("JornadaId", out var id);
        if (zona is string || id is int)
            _viewModel.Preparar(zona as string, id as int?);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await OperacionBaseDatos.EjecutarAsync(_viewModel.CargarAsync);
    }
}
