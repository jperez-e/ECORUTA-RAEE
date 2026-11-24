using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.ViewModels;

public partial class EquipoBorradorViewModel : ObservableObject
{
    private readonly int _id;
    private readonly Action<EquipoBorradorViewModel> _quitar;

    internal EquipoBorradorViewModel(ResiduoElectronico? equipo, Action<EquipoBorradorViewModel> quitar)
    {
        _id = equipo?.Id ?? 0;
        _quitar = quitar;
        categoria = equipo?.Categoria ?? string.Empty;
        descripcion = equipo?.Descripcion ?? string.Empty;
        cantidadTexto = (equipo?.Cantidad ?? 1).ToString(CultureInfo.InvariantCulture);
        pesoTexto = equipo?.PesoEstimado.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
        mostrandoEditor = equipo is null;
    }

    public ReadOnlyCollection<string> Categorias => DatosEquipo.Categorias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo))]
    private string categoria;

    [ObservableProperty]
    private string descripcion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Resumen))]
    private string cantidadTexto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Resumen))]
    private string pesoTexto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoEditor))]
    private bool mostrandoEditor;

    [ObservableProperty] private string errorCategoria = string.Empty;
    [ObservableProperty] private string errorDescripcion = string.Empty;
    [ObservableProperty] private string errorCantidad = string.Empty;
    [ObservableProperty] private string errorPeso = string.Empty;

    public string Titulo => string.IsNullOrWhiteSpace(Categoria) ? "Nuevo equipo" : Categoria;
    public string Resumen
    {
        get
        {
            int unidades = Unidades;
            return $"{unidades} {(unidades == 1 ? "unidad" : "unidades")} · {Peso:F2} kg";
        }
    }
    public string TextoEditor => MostrandoEditor ? "Cerrar editor" : "Editar";
    internal int Unidades
    {
        get
        {
            int valor = DatosEquipo.LeerCantidad(CantidadTexto);
            return DatosEquipo.ErrorCantidad(valor).Length == 0 ? valor : 0;
        }
    }
    internal double Peso
    {
        get
        {
            double valor = DatosEquipo.LeerPeso(PesoTexto);
            return DatosEquipo.ErrorPeso(valor).Length == 0 ? valor : 0;
        }
    }

    partial void OnCategoriaChanged(string value) => ErrorCategoria = string.Empty;
    partial void OnDescripcionChanged(string value) => ErrorDescripcion = string.Empty;
    partial void OnCantidadTextoChanged(string value) => ErrorCantidad = string.Empty;
    partial void OnPesoTextoChanged(string value) => ErrorPeso = string.Empty;

    [RelayCommand]
    private void AlternarEditor() => MostrandoEditor = !MostrandoEditor;

    [RelayCommand]
    private void Quitar() => _quitar(this);

    internal ResiduoElectronico? Validar()
    {
        string detalle = (Descripcion ?? string.Empty).Trim();
        ErrorCategoria = DatosEquipo.ErrorCategoria(Categoria);
        ErrorDescripcion = DatosEquipo.ErrorDescripcion(detalle);
        ErrorCantidad = DatosEquipo.ErrorCantidad(Unidades);
        ErrorPeso = DatosEquipo.ErrorPeso(Peso);
        if (ErrorCategoria.Length + ErrorDescripcion.Length + ErrorCantidad.Length + ErrorPeso.Length > 0)
        {
            MostrandoEditor = true;
            return null;
        }

        return new ResiduoElectronico
        {
            Id = _id, Categoria = Categoria.Trim(), Descripcion = detalle,
            Cantidad = Unidades, PesoEstimado = Peso
        };
    }
}
