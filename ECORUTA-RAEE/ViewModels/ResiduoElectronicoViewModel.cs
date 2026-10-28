using CommunityToolkit.Mvvm.ComponentModel;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.ViewModels;

public partial class ResiduoElectronicoViewModel : ObservableObject
{
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

    public ResiduoElectronico CrearModelo()
    {
        return new ResiduoElectronico
        {
            SolicitudId = SolicitudId,
            Categoria = Categoria.Trim(),
            Descripcion = Descripcion.Trim(),
            Cantidad = Cantidad,
            PesoEstimado = PesoEstimado
        };
    }
}
