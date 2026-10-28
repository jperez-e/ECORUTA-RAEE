using CommunityToolkit.Mvvm.ComponentModel;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.ViewModels;

public partial class JornadaRecoleccionViewModel : ObservableObject
{
    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private string zona = string.Empty;

    [ObservableProperty]
    private DateTime fechaProgramada = DateTime.Today;

    [ObservableProperty]
    private string estado = "Programada";

    public JornadaRecoleccion CrearModelo()
    {
        return new JornadaRecoleccion
        {
            Nombre = Nombre.Trim(),
            Zona = Zona.Trim(),
            FechaProgramada = FechaProgramada,
            Estado = Estado
        };
    }
}
