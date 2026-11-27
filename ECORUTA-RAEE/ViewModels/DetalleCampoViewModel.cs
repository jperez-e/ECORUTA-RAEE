using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;

namespace ECORUTA_RAEE.ViewModels;

internal sealed class DetalleCampoViewModel(int jornadaId, int? solicitudId)
{
    internal int? SolicitudId { get; private set; } = solicitudId;
    internal ConsultaCampo? Vista { get; private set; }

    // La vista anterior se conserva mientras se comprueba la nueva parada.
    internal void Seleccionar(int id)
    {
        SolicitudId = id;
        Vista = null;
    }

    internal async Task<(bool Correcto, ConsultaCampo? Datos)> ActualizarAsync()
    {
        var resultado = await ConsultarAsync();
        if (resultado.Correcto && resultado.Datos is not null)
            Vista = resultado.Datos;
        return resultado;
    }

    internal async Task<(bool Correcto, ConsultaCampo? Datos)> ConsultarAsync()
    {
        ConsultaCampo? datos = null;
        bool correcto = await OperacionBaseDatos.EjecutarAsync(async () =>
            datos = await JornadaService.Instancia.ObtenerConsultaCampoAsync(jornadaId, SolicitudId));
        return (correcto, datos);
    }

    internal static bool Equivalentes(ConsultaCampo? anterior, ConsultaCampo actual) =>
        anterior is not null && anterior.Resumen == actual.Resumen &&
        anterior.Paradas.Select(Valores).SequenceEqual(actual.Paradas.Select(Valores)) &&
        anterior.Equipos.Select(e => (e.Id, e.Categoria, e.Descripcion, e.Cantidad, e.PesoEstimado))
            .SequenceEqual(actual.Equipos.Select(e => (e.Id, e.Categoria, e.Descripcion, e.Cantidad, e.PesoEstimado)));

    private static object Valores(ParadaCampo parada)
    {
        var s = parada.Solicitud;
        return (s.Id, s.CodigoSolicitud, s.Zona, s.DireccionReferencia, s.Latitud, s.Longitud,
            s.NombreContacto, s.TelefonoContacto, s.PesoTotalEstimado, parada.Unidades);
    }
}
