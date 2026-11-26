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

static void Verificar(bool condicion, string mensaje)
{
    if (!condicion)
        throw new InvalidOperationException(mensaje);
}

async Task<SolicitudRecoleccion> CrearPrueba(string zona, double peso, PuntoRecogida? punto)
{
    var solicitud = new SolicitudRecoleccion
    {
        Zona = zona, DireccionReferencia = "Referencia de prueba", PesoTotalEstimado = peso,
        Latitud = punto?.Latitud, Longitud = punto?.Longitud
    };
    await servicio.CrearAsync(solicitud);
    return solicitud;
}

var puntoValido = PuntoRecogida.Crear(18.53, -70.04)!;
var sinPunto = await CrearPrueba("Manoguayabo", 100, null);
var otraZona = await CrearPrueba("Otra zona", 100, puntoValido);
var zonaAjena = await CrearPrueba("Zona fuera del catálogo", 100, puntoValido);
var pendientesManoguayabo = await jornadas.ObtenerVistaPreviaAsync("Manoguayabo");
Verificar(pendientesManoguayabo.Solicitudes.Count == 0 && pendientesManoguayabo.Disponibilidad.Peso == 0 &&
    await jornadas.CrearAsync("Manoguayabo", DateTime.Today, [sinPunto.Id]) is null &&
    !ElegibilidadRecogida.EsElegible(otraZona) && !ElegibilidadRecogida.EsElegible(zonaAjena),
    "Las ubicaciones faltantes y zonas no admitidas deben quedar fuera de ambos umbrales.");
foreach (var (lat, lon) in new (double?, double?)[] { (18.5, null), (null, -70), (91, -70), (18.5, double.NaN) })
{
    try
    {
        await servicio.CrearAsync(new SolicitudRecoleccion { Zona = "Herrera", Latitud = lat, Longitud = lon });
        throw new InvalidOperationException("Se aceptaron coordenadas parciales o inválidas.");
    }
    catch (ArgumentException) { }
}
Verificar(await servicio.CompletarUbicacionAsync(sinPunto, puntoValido) &&
    !await servicio.CompletarUbicacionAsync(sinPunto, puntoValido) &&
    (await jornadas.ObtenerVistaPreviaAsync("Manoguayabo")).Disponibilidad is { Cantidad: 1, Peso: 100, AlcanzaUmbral: true },
    "Completar ubicación debe habilitar el umbral por peso sin sobrescribir una corrección vigente.");

var primera = await CrearPrueba("Pedro Brand", 1, puntoValido);
var segunda = await CrearPrueba("Pedro Brand", 1, puntoValido);
var tercera = await CrearPrueba("Pedro Brand", 1, puntoValido);
var previa = await jornadas.ObtenerVistaPreviaAsync("Pedro Brand");
Verificar(previa.Disponibilidad is { Cantidad: 3, Peso: 3, AlcanzaUmbral: true }, "Debe alcanzarse el umbral por cantidad.");
tercera.Latitud = tercera.Longitud = null;
await servicio.ActualizarAsync(tercera);
Verificar(await jornadas.CrearAsync("Pedro Brand", DateTime.Today, previa.Solicitudes.Select(s => s.Id).ToArray()) is null,
    "Se debe revalidar la elegibilidad dentro de la transacción.");
await servicio.CompletarUbicacionAsync(tercera, puntoValido);
var programada = await jornadas.CrearAsync("Pedro Brand", DateTime.Today, [primera.Id, segunda.Id, tercera.Id])
    ?? throw new InvalidOperationException("No se pudo programar una jornada por cantidad.");
await jornadas.GenerarRutaAsync(programada.Id);
primera.Latitud = primera.Longitud = null;
try
{
    await servicio.ActualizarAsync(primera);
    throw new InvalidOperationException("Se quitó un punto válido asignado.");
}
catch (UbicacionAsignadaException) { }
var asignada = (await servicio.ObtenerListadoOperativoAsync()).Single(s => s.Id == primera.Id);
Verificar(asignada.EnJornadaActiva && !asignada.UbicacionPendiente, "El bloqueo debe preservar las coordenadas.");
var integrada = new SolicitudFormViewModel { FormularioDisponible = true };
await integrada.CargarParaEditarAsync(primera.Id);
integrada.QuitarUbicacionCommand.Execute(null);
integrada.ZonaConfirmada = true;
await integrada.GuardarCommand.ExecuteAsync(null);
Verificar((await servicio.ObtenerPorIdAsync(primera.Id)) is { UbicacionPendiente: false },
    "El formulario integrado también debe impedir quitar el punto.");
primera.Latitud = 18.55;
primera.Longitud = -70.05;
Verificar(await servicio.ActualizarAsync(primera) &&
    (await jornadas.ObtenerDetalleAsync(programada.Id)) is { RutaDesactualizada: true },
    "Una corrección válida debe permitirse e invalidar la ruta.");

var db = await EcoRutaDatabase.Instance.GetConnectionAsync();
// Simula en la base temporal una jornada guardada antes del cambio de elegibilidad.
await db.ExecuteAsync("UPDATE Solicitudes SET Latitud = NULL, Longitud = NULL WHERE Id = ?", segunda.Id);
Verificar((await jornadas.ObtenerDetalleAsync(programada.Id)) is { Estado: JornadaRecoleccion.Programada } historica &&
    MapaJornada.Crear(historica).SinCoordenadas == 1,
    "Una jornada histórica debe continuar visible y advertir la ubicación faltante.");
var resumen = await jornadas.ObtenerPanelAsync();
var ambiental = await EstadisticasAmbientalesService.Instancia.ObtenerAsync();
await gestor.CargarAsync();
Verificar(ambiental.CantidadElegible == resumen.Zonas.Sum(z => z.Cantidad) &&
    ambiental.PesoElegible == resumen.Zonas.Sum(z => z.Peso) &&
    ambiental.UbicacionesPendientes == resumen.UbicacionesPendientes &&
    gestor.Panel?.CantidadElegible == ambiental.CantidadElegible &&
    gestor.Panel?.PesoElegible == ambiental.PesoElegible &&
    ambiental.Indicadores.PesoVigente > ambiental.PesoElegible &&
    await db.ExecuteScalarAsync<int>("PRAGMA user_version") == 4,
    "Los paneles deben compartir disponibilidad y mantener totales generales y esquema.");
Console.WriteLine("APROBADO: elegibilidad, umbrales, protección transaccional, jornadas antiguas y estadísticas.");

var sinPuntoIntegrada = new SolicitudFormViewModel
{
    FormularioDisponible = true, ZonaSeleccionada = "Los Alcarrizos",
    ZonaConfirmada = true, DireccionReferencia = "Referencia sin mapa"
};
sinPuntoIntegrada.AgregarEquipoCommand.Execute(null);
var equipoSinPunto = sinPuntoIntegrada.Equipos.Single();
equipoSinPunto.Categoria = "Monitor";
equipoSinPunto.Descripcion = "Equipo de prueba sin mapa";
equipoSinPunto.PesoTexto = "75";
await sinPuntoIntegrada.GuardarCommand.ExecuteAsync(null);
Verificar((await servicio.ObtenerTodasAsync()).Single(s => s.Zona == "Los Alcarrizos") is
    { UbicacionPendiente: true, PesoTotalEstimado: 75, Estado: "Pendiente" } &&
    (await jornadas.ObtenerVistaPreviaAsync("Los Alcarrizos")).Solicitudes.Count == 0,
    "El formulario integrado debe registrar el alta sin coordenadas y excluirla de la jornada.");
await db.CloseAsync();
var reabierta = await new EcoRutaDatabase(Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory,
    "ecoruta-raee.db3")).GetConnectionAsync();
Verificar(await reabierta.FindAsync<SolicitudRecoleccion>(sinPunto.Id) is { UbicacionPendiente: false } &&
    await reabierta.FindAsync<SolicitudRecoleccion>(segunda.Id) is { UbicacionPendiente: true } &&
    await reabierta.ExecuteScalarAsync<int>("PRAGMA user_version") == 4,
    "Los puntos corregidos y los registros históricos deben mantenerse tras reabrir SQLite.");
Console.WriteLine("APROBADO: alta integrada sin punto y persistencia tras reabrir SQLite v4.");
