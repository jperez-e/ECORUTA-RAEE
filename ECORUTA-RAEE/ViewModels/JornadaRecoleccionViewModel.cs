using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

internal partial class JornadaRecoleccionViewModel : ObservableObject
{
    private string? _zona;
    private int? _jornadaId;

    [ObservableProperty]
    private bool esVistaPrevia;

    [ObservableProperty]
    private ZonaDisponible? disponibilidad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeCancelar), nameof(TieneDetalle))]
    private JornadaRecoleccion? jornada;

    [ObservableProperty]
    private IReadOnlyList<SolicitudRecoleccion> solicitudes = Array.Empty<SolicitudRecoleccion>();

    [ObservableProperty]
    private DateTime fechaProgramada = DateTime.Today;

    [ObservableProperty]
    private string mensaje = string.Empty;

    public bool PuedeCancelar => Jornada?.Estado == JornadaRecoleccion.Programada;
    public bool TieneDetalle => Jornada is not null;

    public void Preparar(string? zona, int? id)
    {
        _zona = zona;
        _jornadaId = id;
    }

    public async Task CargarAsync()
    {
        Mensaje = string.Empty;
        if (_jornadaId is int id)
        {
            MostrarDetalle(await JornadaService.Instancia.ObtenerDetalleAsync(id));
            if (Jornada is null)
                Mensaje = "La jornada ya no está disponible.";
        }
        else if (_zona is not null)
        {
            var vista = await JornadaService.Instancia.ObtenerVistaPreviaAsync(_zona);
            Disponibilidad = vista.Disponibilidad;
            Solicitudes = vista.Solicitudes;
            EsVistaPrevia = true;
        }
    }

    [RelayCommand]
    private Task CrearJornada() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        if (_zona is null || Disponibilidad?.AlcanzaUmbral != true || !EsVistaPrevia)
            return;

        if (FechaProgramada.Date < DateTime.Today)
        {
            Mensaje = "Selecciona la fecha de hoy o una posterior.";
            return;
        }

        var ids = Solicitudes.Select(s => s.Id).ToArray();
        var creada = await JornadaService.Instancia.CrearAsync(_zona, FechaProgramada, ids);
        if (creada is null)
        {
            await CargarAsync();
            Mensaje = "La disponibilidad o los umbrales cambiaron. Revisa las solicitudes actualizadas.";
            return;
        }

        _jornadaId = creada.Id;
        MostrarDetalle(creada);
    });

    [RelayCommand]
    private Task CancelarJornada() => OperacionBaseDatos.EjecutarAsync(async () =>
    {
        if (Jornada is not { Estado: JornadaRecoleccion.Programada } jornada)
            return;

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Cancelar jornada", "Sus solicitudes pendientes volverán a estar disponibles para otra jornada.",
            "Cancelar jornada", "Volver");
        if (!confirmar)
            return;

        bool cancelada = await JornadaService.Instancia.CancelarAsync(jornada.Id);
        await CargarAsync();
        if (!cancelada)
            Mensaje = "La jornada ya no está activa.";
    });

    private void MostrarDetalle(JornadaRecoleccion? detalle)
    {
        Jornada = detalle;
        Solicitudes = detalle?.Solicitudes ?? Array.Empty<SolicitudRecoleccion>();
        EsVistaPrevia = false;
    }
}
