using CommunityToolkit.Mvvm.ComponentModel;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.ViewModels;

public partial class UsuarioViewModel : ObservableObject
{
    [ObservableProperty]
    private string nombre = string.Empty;

    [ObservableProperty]
    private string correo = string.Empty;

    [ObservableProperty]
    private string rol = "Ciudadano";

    public Usuario CrearModelo()
    {
        return new Usuario
        {
            Nombre = Nombre.Trim(),
            Correo = Correo.Trim(),
            Rol = Rol
        };
    }
}
