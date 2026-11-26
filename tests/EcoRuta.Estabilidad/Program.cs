using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;

var servicio = SolicitudService.Instancia;
var creada = new SolicitudRecoleccion
{
    Zona = "Herrera",
    DireccionReferencia = "Referencia original",
    PesoTotalEstimado = 20
};
await servicio.CrearAsync(creada);

async Task<SolicitudFormViewModel> AbrirBorradorAsync()
{
    var vm = new SolicitudFormViewModel { FormularioDisponible = true };
    if (!await vm.CargarParaEditarAsync(creada.Id))
        throw new InvalidOperationException("No se pudo cargar la solicitud de prueba.");
    vm.ZonaConfirmada = true;
    return vm;
}

var primero = await AbrirBorradorAsync();
var segundo = await AbrirBorradorAsync();
primero.DireccionReferencia = "Referencia de la primera edición";
await primero.GuardarCommand.ExecuteAsync(null);

segundo.DireccionReferencia = "Referencia obsoleta de otra pantalla";
segundo.AgregarEquipoCommand.Execute(null);
var equipo = segundo.Equipos.Single();
equipo.Categoria = "Monitor";
equipo.Descripcion = "Equipo de prueba";
equipo.CantidadTexto = "1";
equipo.PesoTexto = "7";
await segundo.GuardarCommand.ExecuteAsync(null);

var (persistida, equipos) = await servicio.ObtenerCompletaAsync(creada.Id);
if (persistida is null || persistida.DireccionReferencia != primero.DireccionReferencia ||
    persistida.PesoTotalEstimado != 20 || equipos.Count != 0)
    throw new InvalidOperationException("Un borrador obsoleto sobrescribió la solicitud o insertó equipos.");

if (!segundo.ErrorEquipos.Contains("otra pantalla") || segundo.Equipos.Single() != equipo ||
    segundo.DireccionReferencia != "Referencia obsoleta de otra pantalla")
    throw new InvalidOperationException("El conflicto debe informar al usuario y conservar su borrador.");

Console.WriteLine("APROBADO: el borrador obsoleto se conserva sin sobrescribir los datos guardados.");

if (!equipo.Resumen.StartsWith("1 unidad ·", StringComparison.Ordinal))
    throw new InvalidOperationException("El resumen debe usar el singular para un equipo.");
equipo.CantidadTexto = "2";
if (!equipo.Resumen.StartsWith("2 unidades ·", StringComparison.Ordinal))
    throw new InvalidOperationException("El resumen debe usar el plural para dos equipos.");
Console.WriteLine("APROBADO: el resumen distingue una unidad de varias unidades.");

foreach (var perfil in new[] { PerfilDemostracion.Ciudadano, PerfilDemostracion.Recolector })
{
    foreach (string ruta in new[] { "//solicitudes", "//residuos", "//jornadas", "//estadisticas",
        "JornadaDetallePage", "ResiduoFormPage", "ResiduosSolicitudPage", "//perfil/%6Aornadas" })
    {
        if (RutasDemostracion.Permite(perfil, ruta) || !RutasDemostracion.Permite(PerfilDemostracion.Gestor, ruta))
            throw new InvalidOperationException("La ruta administrativa debe quedar limitada al gestor.");
    }
}
if (!RutasDemostracion.Permite(PerfilDemostracion.Ciudadano, "SolicitudFormPage") ||
    RutasDemostracion.Permite(PerfilDemostracion.Recolector, "SolicitudFormPage"))
    throw new InvalidOperationException("El recolector no debe acceder al formulario de solicitud.");
Console.WriteLine("APROBADO: separación de rutas directas por perfil.");

var alta = new SolicitudFormViewModel
{
    FormularioDisponible = true, ZonaSeleccionada = "Pantoja",
    DireccionReferencia = "Parque de prueba", VolverAPortada = true
};
alta.AplicarPuntoRecogida(PuntoRecogida.Crear(18.51, -70.01)!);
alta.ZonaConfirmada = true;
alta.AgregarEquipoCommand.Execute(null);
var fila = alta.Equipos.Single();
fila.Categoria = "Monitor";
fila.Descripcion = "Monitores de prueba";
fila.CantidadTexto = "2";
fila.PesoTexto = "50";
await alta.GuardarCommand.ExecuteAsync(null);
var ciudadano = new PortadaDemoViewModel(PerfilDemostracion.Ciudadano);
var gestor = new PortadaDemoViewModel(PerfilDemostracion.Gestor);
if (!await ciudadano.CargarAsync() || !await gestor.CargarAsync() ||
    ciudadano.Solicitudes.Single(s => s.Zona == "Pantoja") is not { PesoTotalEstimado: 50 } registrada ||
    ciudadano.Equipos(registrada.Id).Single().Cantidad != 2 ||
    gestor.Panel?.Indicadores.Pendientes != 2 || gestor.Panel.Alertas.All(a => a.Zona != "Pantoja" || !a.EsLista))
    throw new InvalidOperationException("El gestor y el ciudadano deben leer los mismos datos persistidos.");

var jornadas = JornadaService.Instancia;
var jornada = await jornadas.CrearAsync("Pantoja", DateTime.Today, [registrada.Id]);
if (jornada is null || await jornadas.GenerarRutaAsync(jornada.Id) is null)
    throw new InvalidOperationException("El flujo gestor debe programar y guardar la ruta.");
var recolector = new PortadaDemoViewModel(PerfilDemostracion.Recolector);
if (!await recolector.CargarAsync() ||
    recolector.Jornadas.Single() is not { CantidadSolicitudes: 1, PesoTotal: 50 } disponible ||
    (await jornadas.ObtenerDetalleAsync(disponible.Id))?.Ruta?.Paradas.Single().Solicitud.Id != registrada.Id)
    throw new InvalidOperationException("La consulta de campo debe conservar la jornada, el peso y el orden.");
if (!await jornadas.CancelarAsync(jornada.Id) || !await recolector.CargarAsync() || recolector.Jornadas.Count != 0)
    throw new InvalidOperationException("La portada de campo solo incluye jornadas programadas.");
Console.WriteLine("APROBADO: ciudadano registra, gestor programa y recolector consulta la misma base local.");
