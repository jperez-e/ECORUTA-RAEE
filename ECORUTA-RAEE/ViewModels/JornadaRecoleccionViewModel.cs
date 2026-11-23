using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.Views;

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
    [NotifyPropertyChangedFor(nameof(PuedeCancelar), nameof(TieneDetalle), nameof(PuedePlanificar))]
    private JornadaRecoleccion? jornada;

    private IReadOnlyList<SolicitudRecoleccion> _solicitudes = Array.Empty<SolicitudRecoleccion>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListaParadasTitulo))]
    private IReadOnlyList<ParadaRuta> paradas = Array.Empty<ParadaRuta>();

    [ObservableProperty]
    private string estadoRuta = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayCoordenadasIncompletas))]
    private int coordenadasIncompletas;

    [ObservableProperty]
    private string accionRuta = "Generar orden sugerido";

    [ObservableProperty]
    private DateTime fechaProgramada = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    private string mensaje = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeInteractuar))]
    private bool ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayConfirmacion))]
    private string confirmacion = string.Empty;

    public bool PuedeCancelar => Jornada?.Estado == JornadaRecoleccion.Programada;
    public bool TieneDetalle => Jornada is not null;
    public bool PuedePlanificar => PuedeCancelar && Jornada!.CantidadSolicitudes > 0;
    public bool HayCoordenadasIncompletas => CoordenadasIncompletas > 0;
    public bool HayConfirmacion => Confirmacion.Length > 0;
    public bool HayMensaje => Mensaje.Length > 0;
    public bool PuedeInteractuar => !Ocupado;
    public string ListaParadasTitulo => Paradas.Count > 0 && Paradas[0].Orden > 0
        ? "Paradas en orden sugerido" : "Solicitudes de la jornada";

    partial void OnFechaProgramadaChanged(DateTime value) => Mensaje = string.Empty;

    public void Preparar(string? zona, int? id)
    {
        _zona = zona;
        _jornadaId = id;
    }

    public Task CargarPantallaAsync() => EjecutarAsync(CargarAsync);

    private async Task CargarAsync()
    {
        Mensaje = string.Empty;
        Confirmacion = string.Empty;
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
            _solicitudes = vista.Solicitudes;
            Paradas = FilasSinPlan();
            EsVistaPrevia = true;
        }
    }

    [RelayCommand]
    private Task CrearJornada() => EjecutarAsync(async () =>
    {
        if (_zona is null || Disponibilidad?.AlcanzaUmbral != true || !EsVistaPrevia)
            return;

        if (FechaProgramada.Date < DateTime.Today)
        {
            Mensaje = "Selecciona la fecha de hoy o una posterior.";
            return;
        }

        var ids = _solicitudes.Select(s => s.Id).ToArray();
        var fecha = FechaProgramada;
        bool confirmada = await Shell.Current.DisplayAlertAsync(
            "Confirmar jornada",
            $"Se asignarán {ids.Length} solicitudes de {_zona} para el {fecha:dd/MM/yyyy}.",
            "Programar jornada", "Revisar");
        if (!confirmada)
            return;

        var creada = await JornadaService.Instancia.CrearAsync(_zona, fecha, ids);
        if (creada is null)
        {
            await CargarAsync();
            Mensaje = "La disponibilidad o los umbrales cambiaron. Revisa las solicitudes actualizadas.";
            return;
        }

        _jornadaId = creada.Id;
        MostrarDetalle(creada);
        Confirmacion = "Jornada programada.";
    });

    [RelayCommand]
    private Task PlanificarRuta() => EjecutarAsync(async () =>
    {
        if (Jornada is not { Estado: JornadaRecoleccion.Programada } jornada)
            return;

        var actualizada = await JornadaService.Instancia.GenerarRutaAsync(jornada.Id);
        if (actualizada is null)
        {
            await CargarAsync();
            Mensaje = "La jornada ya no tiene solicitudes disponibles para planificar.";
            return;
        }

        Mensaje = string.Empty;
        MostrarDetalle(actualizada);
        Confirmacion = "Orden sugerido guardado.";
    });

    [RelayCommand]
    private async Task EditarSolicitud(SolicitudRecoleccion? solicitud)
    {
        if (solicitud is null || !PuedePlanificar || Ocupado)
            return;

        await Shell.Current.GoToAsync(nameof(SolicitudFormPage), new ShellNavigationQueryParameters
        {
            ["SolicitudId"] = solicitud.Id,
            ["VolverAJornada"] = true
        });
    }

    [RelayCommand]
    private Task CancelarJornada() => EjecutarAsync(async () =>
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
        else
            Confirmacion = "Jornada cancelada. Sus solicitudes pendientes vuelven a estar disponibles.";
    });

    private async Task EjecutarAsync(Func<Task> operacion)
    {
        if (Ocupado)
            return;

        Ocupado = true;
        Confirmacion = string.Empty;
        try { await OperacionBaseDatos.EjecutarAsync(operacion); }
        finally { Ocupado = false; }
    }

    private void MostrarDetalle(JornadaRecoleccion? detalle)
    {
        Jornada = detalle;
        _solicitudes = detalle?.Solicitudes ?? Array.Empty<SolicitudRecoleccion>();
        var ruta = detalle?.Ruta;
        Paradas = ruta?.Paradas ?? FilasSinPlan();
        CoordenadasIncompletas = _solicitudes.Count(s => !Coordenadas.SonValidas(s));
        EstadoRuta = ruta?.Metodo switch
        {
            MetodoRuta.VecinoMasCercano => "Ruta sugerida por proximidad geográfica. Los tramos indican distancias en línea recta.",
            MetodoRuta.Antiguedad => "Orden alternativo por antigüedad, según fecha de registro y código.",
            _ => detalle?.RutaDesactualizada == true
                ? "Los datos de las paradas cambiaron. Regenera el orden sugerido."
                : string.Empty
        };
        AccionRuta = ruta is not null || detalle?.RutaDesactualizada == true
            ? "Regenerar orden sugerido"
            : "Generar orden sugerido";
        EsVistaPrevia = false;
    }

    private IReadOnlyList<ParadaRuta> FilasSinPlan() =>
        _solicitudes.Select(s => new ParadaRuta(s, 0, null)).ToList();
}
