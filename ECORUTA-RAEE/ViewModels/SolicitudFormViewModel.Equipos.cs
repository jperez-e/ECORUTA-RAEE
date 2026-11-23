using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;

namespace ECORUTA_RAEE.ViewModels;

public partial class SolicitudFormViewModel
{
    private readonly ObservableCollection<EquipoBorradorViewModel> _equipos = new();
    private IReadOnlyList<ResiduoElectronico> _equiposOriginales = Array.Empty<ResiduoElectronico>();
    private double _pesoOriginal;

    public SolicitudFormViewModel() => Equipos = new(_equipos);

    public ReadOnlyObservableCollection<EquipoBorradorViewModel> Equipos { get; }
    public int RegistrosEquipos => _equipos.Count;
    public long UnidadesEquipos => _equipos.Sum(e => (long)e.Unidades);
    public bool SinEquipos => _equipos.Count == 0;
    public double PesoResumen => PesoEditable ? PesoManualParaResumen() : _equipos.Sum(e => e.Peso);
    public string ZonaResumen => ZonaConfirmada ? ZonaSeleccionada ?? string.Empty : "Pendiente de confirmar";

    [ObservableProperty] private string errorEquipos = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeEditarFormulario))]
    private bool guardando;

    public bool PuedeEditarFormulario => FormularioDisponible && !Guardando;

    [RelayCommand]
    private void AgregarEquipo() => AgregarAlBorrador(null);

    private void AgregarAlBorrador(ResiduoElectronico? equipo)
    {
        if (Guardando)
            return;

        var fila = new EquipoBorradorViewModel(equipo, QuitarEquipo);
        fila.PropertyChanged += EquipoCambiado;
        _equipos.Add(fila);
        ActualizarResumenEquipos();
    }

    private void QuitarEquipo(EquipoBorradorViewModel fila)
    {
        if (Guardando)
            return;

        fila.PropertyChanged -= EquipoCambiado;
        _equipos.Remove(fila);
        ActualizarResumenEquipos();
    }

    private void CargarEquipos(IReadOnlyList<ResiduoElectronico> originales, double peso)
    {
        foreach (var fila in _equipos)
            fila.PropertyChanged -= EquipoCambiado;
        _equipos.Clear();
        _equiposOriginales = originales;
        _pesoOriginal = peso;
        foreach (var equipo in originales)
            AgregarAlBorrador(equipo);
        ActualizarResumenEquipos();
    }

    private void EquipoCambiado(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EquipoBorradorViewModel.CantidadTexto) or nameof(EquipoBorradorViewModel.PesoTexto))
            ActualizarResumenEquipos();
    }

    private void ActualizarResumenEquipos()
    {
        ErrorEquipos = string.Empty;
        OnPropertyChanged(nameof(RegistrosEquipos));
        OnPropertyChanged(nameof(UnidadesEquipos));
        OnPropertyChanged(nameof(SinEquipos));
        OnPropertyChanged(nameof(PesoEditable));
        OnPropertyChanged(nameof(PesoResumen));
    }

    private double PesoManualParaResumen()
    {
        double peso = DatosEquipo.LeerPeso(PesoTexto);
        return double.IsFinite(peso) && peso >= 0 ? peso : 0;
    }
}
