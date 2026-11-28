using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using System.Net;
using System.Text.Json;

await VerificarZonasAsync();
var auth = Autenticacion.Instancia;
string claveGestor = Guid.NewGuid().ToString("N");
string claveCiudadana = Guid.NewGuid().ToString("N");
const string correoGestor = "gestion@example.invalid";
const string correoCiudadano = "ciudadania@example.invalid";
await auth.AprovisionarAsync(new("Gestión de pruebas", correoGestor, null), claveGestor);
await auth.RegistrarAsync(new("Ciudadanía de pruebas", correoCiudadano, null), claveCiudadana);
Verificar((await auth.EntrarAsync(correoGestor, claveGestor, false)).Identidad?.Rol == RolUsuario.Gestor, "Login gestor.");

var vacioGestion = await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync();
Verificar(vacioGestion.Panel.Indicadores is { Solicitudes: 0, PesoVigente: 0, UnidadesEquipos: 0 } &&
    vacioGestion.Alertas.Count == 0 && (await SolicitudService.Instancia.ObtenerGestionAsync()).Count == 0,
    "El centro de operaciones vacío no debe inventar cifras ni alertas.");
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

foreach (var perfil in new[] { RolUsuario.Ciudadano, RolUsuario.Recolector })
{
    foreach (string ruta in new[] { "//solicitudes", "//residuos", "//jornadas", "//estadisticas",
        "JornadaDetallePage", "ResiduoFormPage", "ResiduosSolicitudPage", "//perfil/%6Aornadas" })
    {
        if (RutasCuenta.Permite(perfil, ruta) || !RutasCuenta.Permite(RolUsuario.Gestor, ruta))
            throw new InvalidOperationException("La ruta administrativa debe quedar limitada al gestor.");
    }
}
if (!RutasCuenta.Permite(RolUsuario.Ciudadano, "SolicitudFormPage") ||
    RutasCuenta.Permite(RolUsuario.Recolector, "SolicitudFormPage"))
    throw new InvalidOperationException("El recolector no debe acceder al formulario de solicitud.");
Console.WriteLine("APROBADO: separación de rutas directas por perfil.");

await auth.SalirAsync();
Verificar((await auth.EntrarAsync(correoCiudadano, claveCiudadana, false)).Identidad?.Rol == RolUsuario.Ciudadano, "Login ciudadano.");
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
var ciudadano = new PortadaDemoViewModel(RolUsuario.Ciudadano);
var gestor = new PortadaDemoViewModel(RolUsuario.Gestor);
if (!await ciudadano.CargarAsync() ||
    ciudadano.Solicitudes.Single(s => s.Zona == "Pantoja") is not { PesoTotalEstimado: 50 } registrada ||
    ciudadano.Equipos(registrada.Id).Single().Cantidad != 2 || ciudadano.Solicitudes.Count != 1)
    throw new InvalidOperationException("El gestor y el ciudadano deben leer los mismos datos persistidos.");

await auth.SalirAsync();
await auth.EntrarAsync(correoGestor, claveGestor, false);
Verificar(await gestor.CargarAsync() && gestor.Panel?.Indicadores.Pendientes == 2 &&
    gestor.Panel.Alertas.Any(a => a.Zona == "Pantoja" && a.EsLista), "Gestión ve solicitudes propias e históricas.");
var jornadas = JornadaService.Instancia;
var jornada = await jornadas.CrearAsync("Pantoja", DateTime.Today, [registrada.Id]);
if (jornada is null || await jornadas.GenerarRutaAsync(jornada.Id) is null)
    throw new InvalidOperationException("El flujo gestor debe programar y guardar la ruta.");
var recolector = new PortadaDemoViewModel(RolUsuario.Recolector);
if (!await recolector.CargarAsync() ||
    recolector.PortadaCampo.Single() is not { Paradas: 1, Peso: 50 } disponible ||
    (await jornadas.ObtenerDetalleAsync(disponible.Id))?.Ruta?.Paradas.Single().Solicitud.Id != registrada.Id)
    throw new InvalidOperationException("La consulta de campo debe conservar la jornada, el peso y el orden.");
if (!await jornadas.CancelarAsync(jornada.Id) || !await recolector.CargarAsync() || recolector.PortadaCampo.Count != 0)
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
var alertasPendientes = (await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync()).Alertas;
Verificar(alertasPendientes.Any(a => a.JornadaId == programada.Id && a.Ruta == AlertaGestion.RutaJornada &&
    a.Clave == "JornadaId" && Equals(a.Valor, programada.Id)) &&
    alertasPendientes.Any(a => a.Filtro == FiltroGestion.ProgramadasSinTelefono &&
        a.Ruta == AlertaGestion.RutaSolicitudes && a.Clave == "Filtro"),
    "Las alertas deben señalar la jornada y el filtro reales.");
var vistaPendiente = await jornadas.ObtenerConsultaCampoAsync(programada.Id);
Verificar(vistaPendiente is { OrdenVigente: false } && vistaPendiente.Paradas.Count == 3 &&
    vistaPendiente.Resumen.EstadoOrden == "Orden de paradas pendiente",
    "Una jornada sin ruta debe ofrecer las solicitudes sin un orden vigente.");
Verificar((await jornadas.ObtenerPortadaCampoAsync()).Any(j => j.Id == programada.Id && j.Paradas == 3),
    "La portada debe leer paradas de jornadas activas.");
await jornadas.GenerarRutaAsync(programada.Id);
var vistaOrdenada = (await jornadas.ObtenerConsultaCampoAsync(programada.Id))!;
var ordenGuardado = (await jornadas.ObtenerDetalleAsync(programada.Id))!.Ruta!.Paradas.Select(p => p.Solicitud.Id);
Verificar(vistaOrdenada.OrdenVigente && vistaOrdenada.Paradas.Select(p => p.Solicitud.Id).SequenceEqual(ordenGuardado),
    "El orden de consulta debe ser idéntico al persistido.");
int inicialCampo = vistaOrdenada.Paradas[0].Solicitud.Id;
int intermediaCampo = vistaOrdenada.Paradas[1].Solicitud.Id;
int ultimaCampo = vistaOrdenada.Paradas[^1].Solicitud.Id;
Verificar(vistaOrdenada.Adyacente(inicialCampo, -1) is null &&
    vistaOrdenada.Adyacente(inicialCampo, 1) == intermediaCampo &&
    vistaOrdenada.Adyacente(intermediaCampo, -1) == inicialCampo &&
    vistaOrdenada.Adyacente(intermediaCampo, 1) == ultimaCampo &&
    vistaOrdenada.Adyacente(ultimaCampo, 1) is null,
    "Anterior y Siguiente deben respetar el orden y sus límites.");
var consultaRecolector = new DetalleCampoViewModel(programada.Id, primera.Id);
var antesContactoCampo = (await consultaRecolector.ActualizarAsync()).Datos;
Verificar(antesContactoCampo is not null && antesContactoCampo.Paradas[antesContactoCampo.Indice(primera.Id)]
    .Solicitud.TelefonoContacto is null, "Debe tolerar ausencia de contacto.");
var contacto = new SolicitudFormViewModel { FormularioDisponible = true };
var contactoObsoleto = new SolicitudFormViewModel { FormularioDisponible = true };
await contacto.CargarParaEditarAsync(primera.Id);
await contactoObsoleto.CargarParaEditarAsync(primera.Id);
contacto.NombreContacto = "  Contacto técnico  ";
contacto.PrefijoContacto = DatosContacto.Dominicano;
contacto.TelefonoContacto = "809 555 01";
contacto.ZonaConfirmada = true;
await contacto.GuardarCommand.ExecuteAsync(null);
Verificar(contacto.ErrorTelefonoContacto.Length > 0 &&
    (await servicio.ObtenerPorIdAsync(primera.Id))?.TelefonoContacto is null,
    "Un teléfono incompleto debe producir error inline sin escritura.");
contacto.TelefonoContacto = "809 555 0100";
contacto.AplicarUbicacion(SeleccionUbicacion.Crear(puntoValido, "Pedro Brand", null)!);
Verificar(contacto.TelefonoContacto == "809 555 0100", "El mapa debe conservar el contacto en el borrador.");
await contacto.GuardarCommand.ExecuteAsync(null);
Verificar((await servicio.ObtenerPorIdAsync(primera.Id)) is
    { NombreContacto: "Contacto técnico", TelefonoContacto: "+18095550100" } &&
    (await jornadas.ObtenerDetalleAsync(programada.Id)) is { RutaDesactualizada: false, Ruta: not null },
    "El contacto debe guardarse sin invalidar la ruta programada.");
var despuesContactoCampo = (await consultaRecolector.ActualizarAsync()).Datos!;
Verificar(!DetalleCampoViewModel.Equivalentes(antesContactoCampo, despuesContactoCampo) &&
    despuesContactoCampo.Paradas[despuesContactoCampo.Indice(primera.Id)].Solicitud.TelefonoContacto == "+18095550100" &&
    despuesContactoCampo.Paradas.Where(p => p.Solicitud.Id != primera.Id).All(p => p.Solicitud.TelefonoContacto is null),
    "Una nueva consulta debe detectar contacto cambiado y limitar la carga a la parada visible.");
contactoObsoleto.NombreContacto = "Borrador de prueba";
contactoObsoleto.ZonaConfirmada = true;
await contactoObsoleto.GuardarCommand.ExecuteAsync(null);
Verificar(contactoObsoleto.NombreContacto == "Borrador de prueba" &&
    (await servicio.ObtenerPorIdAsync(primera.Id))?.NombreContacto == "Contacto técnico",
    "Un borrador obsoleto de contacto no debe sobrescribir los datos.");
Console.WriteLine("APROBADO: validación inline, borrador, concurrencia y ruta vigente después de editar contacto.");
primera = (await servicio.ObtenerPorIdAsync(primera.Id))!;
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
var desactualizadaCampo = await jornadas.ObtenerConsultaCampoAsync(programada.Id, segunda.Id);
Verificar(desactualizadaCampo is { OrdenVigente: false } &&
    desactualizadaCampo.Resumen.EstadoOrden == "Orden desactualizado" &&
    !Coordenadas.SonValidas(desactualizadaCampo.Paradas[desactualizadaCampo.Indice(segunda.Id)].Solicitud) &&
    await jornadas.ObtenerConsultaCampoAsync(programada.Id, int.MaxValue) is null,
    "Una parada sin punto o ajena a la jornada no debe proporcionar un destino.");
var alertasHistoricas = (await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync()).Alertas
    .Where(a => a.JornadaId == programada.Id).ToArray();
Verificar(alertasHistoricas.Length == 1 && alertasHistoricas[0].Detalle.Contains("Orden desactualizado") &&
    alertasHistoricas[0].Detalle.Contains("sin punto"),
    "Una sola alerta por jornada debe reunir el orden desactualizado y los puntos faltantes.");
var asignadaSinPunto = await servicio.ObtenerPorIdAsync(segunda.Id)
    ?? throw new InvalidOperationException("Falta la solicitud histórica.");
Verificar(await servicio.TieneAsignacionActivaAsync(segunda.Id) &&
    !await servicio.CompletarUbicacionAsync(asignadaSinPunto, new SeleccionUbicacion(puntoValido, "Pantoja", null)) &&
    (await servicio.ObtenerPorIdAsync(segunda.Id)) is { Zona: "Pedro Brand", UbicacionPendiente: true },
    "Completar ubicación no debe cambiar la zona asignada dentro de la transacción.");
var resumen = await jornadas.ObtenerPanelAsync();
var ambiental = await EstadisticasAmbientalesService.Instancia.ObtenerAsync();
await gestor.CargarAsync();
Verificar(ambiental.CantidadElegible == resumen.Zonas.Sum(z => z.Cantidad) &&
    ambiental.PesoElegible == resumen.Zonas.Sum(z => z.Peso) &&
    ambiental.UbicacionesPendientes == resumen.UbicacionesPendientes &&
    gestor.Panel?.CantidadElegible == ambiental.CantidadElegible &&
    gestor.Panel?.PesoElegible == ambiental.PesoElegible &&
    ambiental.Indicadores.PesoVigente > ambiental.PesoElegible &&
    await db.ExecuteScalarAsync<int>("PRAGMA user_version") == 6,
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
var altaConContacto = new SolicitudFormViewModel
{
    FormularioDisponible = true, ZonaSeleccionada = "Herrera", ZonaConfirmada = true,
    DireccionReferencia = "Referencia de prueba de contacto", NombreContacto = "Contacto de prueba",
    PrefijoContacto = DatosContacto.Dominicano, TelefonoContacto = "849 555 0100"
};
altaConContacto.AgregarEquipoCommand.Execute(null);
var equipoContacto = altaConContacto.Equipos.Single();
equipoContacto.Categoria = "Monitor";
equipoContacto.Descripcion = "Monitor de prueba";
equipoContacto.PesoTexto = "8";
await altaConContacto.GuardarCommand.ExecuteAsync(null);
Verificar((await servicio.ObtenerTodasAsync()).SingleOrDefault(s => s.NombreContacto == "Contacto de prueba") is
    { TelefonoContacto: "+18495550100", PesoTotalEstimado: 8 },
    "El alta integrada debe persistir contacto y equipos juntos.");
Console.WriteLine("APROBADO: alta integrada con contacto opcional normalizado.");
await db.CloseAsync();
var reabierta = await new EcoRutaDatabase(Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory,
    "ecoruta-raee.db3")).GetConnectionAsync();
Verificar(await reabierta.FindAsync<SolicitudRecoleccion>(sinPunto.Id) is { UbicacionPendiente: false } &&
    await reabierta.FindAsync<SolicitudRecoleccion>(segunda.Id) is { UbicacionPendiente: true } &&
    await reabierta.ExecuteScalarAsync<int>("PRAGMA user_version") == 6,
    "Los puntos corregidos y los registros históricos deben mantenerse tras reabrir SQLite.");
Console.WriteLine("APROBADO: alta integrada sin punto y persistencia tras reabrir SQLite v6.");

Verificar(await reabierta.FindAsync<SolicitudRecoleccion>(primera.Id) is { TelefonoContacto: "+18095550100" },
    "El contacto se conserva al reabrir.");
await reabierta.CloseAsync();
await VerificarMigracionContactoAsync();
var gestion = new SolicitudesViewModel();
await gestion.CargarSolicitudesAsync();
gestion.AbrirFiltro(FiltroGestion.Programadas);
gestion.TextoBusqueda = primera.CodigoSolicitud;
var filaGestion = gestion.Solicitudes.Single();
Verificar(filaGestion.FechaProgramada == DateTime.Today && filaGestion.TieneTelefono &&
    filaGestion.TelefonoContacto is null && filaGestion.NombreContacto is null,
    "El listado administrativo debe mostrar fecha y disponibilidad sin cargar el contacto completo.");
await gestion.CargarSolicitudesAsync();
Verificar(ReferenceEquals(filaGestion, gestion.Solicitudes.Single()) &&
    gestion.TextoBusqueda == primera.CodigoSolicitud && gestion.FiltroSeleccionado == (int)FiltroGestion.Programadas,
    "Volver de un detalle sin cambios debe conservar la lista, filtro y búsqueda.");
await gestion.EditarCommand.ExecuteAsync(filaGestion);
Verificar(Shell.Current.UltimaRuta == "SolicitudFormPage" &&
    Shell.Current.UltimosParametros?["VolverAListadoGestion"] is true &&
    Equals(Shell.Current.UltimosParametros["SolicitudId"], filaGestion.Id),
    "Editar debe regresar al listado con el contexto actual.");
gestion.AbrirFiltro(FiltroGestion.SinUbicacion);
Verificar(gestion.Solicitudes.Any(s => s.Id == segunda.Id) &&
    gestion.Solicitudes.All(s => s.UbicacionPendiente), "El filtro sin ubicación debe incluir las paradas históricas.");
gestion.AbrirFiltro(FiltroGestion.ProgramadasSinTelefono);
Verificar(gestion.Solicitudes.Count > 0 &&
    gestion.Solicitudes.All(s => s.EnJornadaActiva && !s.TieneTelefono), "El filtro para contacto no cambia elegibilidad.");
var panelGestion = await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync();
Verificar(panelGestion.Panel.Indicadores.Solicitudes == (await servicio.ObtenerTodasAsync()).Count &&
    panelGestion.Panel.Indicadores.UnidadesEquipos == panelGestion.Panel.Categorias.Sum(c => c.Unidades) &&
    Math.Abs(panelGestion.Panel.Categorias.Sum(c => c.ProporcionEquipos) - 1) < 1e-9 &&
    (await jornadas.ObtenerDetalleAsync(programada.Id, incluirUnidades: true))!.UnidadesEquipos ==
    (await servicio.ObtenerGestionAsync()).Where(s => s.FechaProgramada.HasValue).Sum(s => s.Unidades),
    "Los totales administrativos y la dona deben coincidir con SQLite.");
var publicada = (await servicio.ObtenerVistaCiudadanaAsync(primera.Id)).Single();
Verificar(publicada.FechaProgramada == DateTime.Today && publicada.EstadoVisual == "Programada",
    "La vista ciudadana debe consultar la fecha de la jornada activa.");
Verificar(await jornadas.CancelarAsync(programada.Id), "Se debe poder cancelar la jornada de prueba.");
Verificar(await jornadas.ObtenerConsultaCampoAsync(programada.Id, primera.Id) is null &&
    (await jornadas.ObtenerPortadaCampoAsync()).All(j => j.Id != programada.Id),
    "Las consultas y acciones de campo deben excluir jornadas canceladas.");
Console.WriteLine("APROBADO: jornadas de campo, orden persistido, adyacencia, cambios y protección de acciones.");
await gestion.CargarSolicitudesAsync();
Verificar(gestion.Solicitudes.Count == 0 && gestion.FiltroSeleccionado == (int)FiltroGestion.ProgramadasSinTelefono &&
    (await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync()).Alertas.All(a => a.JornadaId != programada.Id),
    "La cancelación debe actualizar filtros y alertas sin reiniciar el filtro.");
var liberada = (await servicio.ObtenerVistaCiudadanaAsync(primera.Id)).Single();
Verificar(liberada.FechaProgramada is null && liberada.EstadoVisual == "En espera",
    "Una jornada cancelada no debe mostrar fecha activa después de volver a consultar.");
Verificar((await servicio.ObtenerVistaCiudadanaAsync(segunda.Id)).Single().EstadoVisual == "Ubicación pendiente",
    "La solicitud histórica sin coordenadas conserva su siguiente paso.");
var otra = await CrearPrueba("Pantoja", 1, puntoValido);
otra.Zona = "Otra zona";
otra.SectorOtraZona = "Sector de prueba";
await servicio.ActualizarAsync(otra);
Verificar((await servicio.ObtenerVistaCiudadanaAsync(otra.Id)).Single().EstadoVisual == "Fuera de agrupación",
    "Otra zona se explica con sus datos existentes.");

await gestion.CargarSolicitudesAsync();
gestion.AbrirFiltro(FiltroGestion.OtraZona);
Verificar(gestion.Solicitudes.Any(s => s.Id == otra.Id) &&
    gestion.Solicitudes.All(s => s.Zona == "Otra zona"), "Otra zona debe tener filtro propio.");
gestion.AbrirFiltro(FiltroGestion.Disponibles);
Verificar(gestion.Solicitudes.All(ElegibilidadRecogida.EsElegible) &&
    gestion.Solicitudes.Any(s => s.Id == primera.Id), "El filtro disponible debe reutilizar la elegibilidad.");
Console.WriteLine("APROBADO: centro de operaciones, destinos, filtros, datos privados y actualización de indicadores.");
var confirmacion = new SolicitudFormViewModel { FormularioDisponible = true, VolverAPortada = true };
confirmacion.AplicarUbicacion(SeleccionUbicacion.Crear(puntoValido, "Herrera", null)!);
confirmacion.DireccionReferencia = "Referencia de la confirmación";
confirmacion.NombreContacto = "Contacto de revisión";
confirmacion.AgregarEquipoCommand.Execute(null);
var filaConfirmacion = confirmacion.Equipos.Single();
filaConfirmacion.Categoria = "Monitor";
filaConfirmacion.Descripcion = "Equipo del borrador";
filaConfirmacion.CantidadTexto = "2";
filaConfirmacion.PesoTexto = "8";
Verificar(confirmacion.UnidadesEquipos == 2 && confirmacion.PesoResumen == 8 &&
    filaConfirmacion.PesoPromedio.Contains('4') && confirmacion.ResumenContacto.Contains("Contacto de revisión"),
    "El resumen debe usar el mismo borrador y distinguir el peso conjunto del promedio.");
int confirmaciones = 0;
confirmacion.MostrarConfirmacionCiudadana = async ficha =>
{
    confirmaciones++;
    Verificar(confirmacion.SalidaConfirmada &&
        (await servicio.ObtenerVistaCiudadanaAsync(ficha.Solicitud.Id)).Single().Solicitud.NombreContacto == "Contacto de revisión",
        "La confirmación solo debe emitirse después de confirmar SQLite.");
};
confirmacion.PersonalizarCodigo = true;
confirmacion.CodigoTexto = creada.CodigoSolicitud;
await confirmacion.GuardarCommand.ExecuteAsync(null);
Verificar(confirmaciones == 0 && confirmacion.ErrorCodigo.Length > 0 &&
    confirmacion.Equipos.Single() == filaConfirmacion && confirmacion.NombreContacto == "Contacto de revisión",
    "El código duplicado debe dejar el borrador visible y no emitir confirmación.");
confirmacion.PersonalizarCodigo = false;
await Task.WhenAll(confirmacion.GuardarCommand.ExecuteAsync(null), confirmacion.GuardarCommand.ExecuteAsync(null));
await confirmacion.GuardarCommand.ExecuteAsync(null);
Verificar(confirmaciones == 1 &&
    (await servicio.ObtenerVistaCiudadanaAsync()).Count(x => x.Solicitud.NombreContacto == "Contacto de revisión") == 1,
    "Las pulsaciones repetidas solo deben producir una solicitud y una confirmación.");
Console.WriteLine("APROBADO: próximo paso, fecha activa, cancelación, resumen, UNIQUE y confirmación única posterior al guardado.");


var eliminableCampo = await CrearPrueba("Manoguayabo", 60, puntoValido);
var previaCampo = await jornadas.ObtenerVistaPreviaAsync("Manoguayabo");
var jornadaCampo = await jornadas.CrearAsync("Manoguayabo", DateTime.Today,
    previaCampo.Solicitudes.Select(s => s.Id).ToArray())
    ?? throw new InvalidOperationException("No se pudo programar la jornada para probar la eliminación.");
await jornadas.GenerarRutaAsync(jornadaCampo.Id);
Verificar(await jornadas.ObtenerConsultaCampoAsync(jornadaCampo.Id, eliminableCampo.Id) is { OrdenVigente: true } conDestino &&
    Coordenadas.SonValidas(conDestino.Paradas[conDestino.Indice(eliminableCampo.Id)].Solicitud) &&
    await servicio.EliminarAsync(eliminableCampo.Id) &&
    await jornadas.ObtenerConsultaCampoAsync(jornadaCampo.Id, eliminableCampo.Id) is null,
    "Una parada eliminada no debe conservar un destino operativo.");
Console.WriteLine("APROBADO: destino válido y revocación de consulta al eliminar una parada.");

var ajusteGestion = await CrearPrueba("Los Alcarrizos", 42, puntoValido);
await CrearPrueba("Herrera", 55, puntoValido);
var actualizadoGestion = await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync();
Verificar(actualizadoGestion.Alertas.Any(a => a.Zona == "Los Alcarrizos" &&
    a.Accion == "Ver disponibilidad" && a.Clave == "Zona" && Equals(a.Valor, "Los Alcarrizos")) &&
    actualizadoGestion.Alertas.Any(a => a.Zona == "Herrera" && a.Accion == "Programar jornada"),
    "Las zonas listas por peso y las próximas deben abrir la vista previa correcta.");
Verificar(actualizadoGestion.Alertas.All(a => RutasCuenta.Permite(RolUsuario.Gestor, a.Ruta) &&
    !RutasCuenta.Permite(RolUsuario.Ciudadano, a.Ruta) &&
    !RutasCuenta.Permite(RolUsuario.Recolector, a.Ruta)),
    "Los destinos operativos deben seguir restringidos al gestor.");
Verificar(Math.Abs(actualizadoGestion.Panel.Indicadores.PesoVigente -
    (await servicio.ObtenerTodasAsync()).Sum(s => s.PesoTotalEstimado)) < 1e-9,
    "El peso vigente debe reflejar cambios sin sumar de nuevo el peso de los residuos.");
ajusteGestion.PesoTotalEstimado = 55;
Verificar(await servicio.ActualizarAsync(ajusteGestion), "El peso de una solicitud manual debe editarse.");
var trasAjusteGestion = await EstadisticasAmbientalesService.Instancia.ObtenerGestionAsync();
Verificar(Math.Abs(trasAjusteGestion.Panel.Indicadores.PesoVigente -
    actualizadoGestion.Panel.Indicadores.PesoVigente - 13) < 1e-9 &&
    trasAjusteGestion.Alertas.Any(a => a.Zona == "Los Alcarrizos" && a.Accion == "Programar jornada"),
    "Al editar el peso, el centro debe actualizar el total y promover una zona próxima a lista.");
Console.WriteLine("APROBADO: alertas por zona, destinos protegidos y peso vigente sin doble contabilización.");

await VerificarCuentasAsync(correoGestor, claveGestor, correoCiudadano, claveCiudadana, creada.Id, registrada.Id);

static void QuitarEsquemaUsuarios(SQLite.SQLiteConnection db)
{
    db.Execute("DROP INDEX IX_Solicitudes_UsuarioId");
    db.Execute("ALTER TABLE Solicitudes DROP COLUMN UsuarioId");
    db.Execute("DROP TABLE Sesiones");
    db.Execute("DROP TABLE Usuarios");
}

static async Task VerificarCuentasAsync(string correoGestor, string claveGestor, string correoA,
    string claveA, int idHistorico, int idA)
{
    var auth = Autenticacion.Instancia;
    var solicitudes = SolicitudService.Instancia;
    var jornadas = JornadaService.Instancia;
    var database = EcoRutaDatabase.Instance;
    var db = await database.GetConnectionAsync();
    string claveB = Guid.NewGuid().ToString("N");
    string claveCampo = Guid.NewGuid().ToString("N");
    string nuevaClave = Guid.NewGuid().ToString("N");
    const string correoB = "segunda@example.invalid";
    const string correoCampo = "campo@example.invalid";

    async Task Denegar(Func<Task> accion)
    {
        try { await accion(); }
        catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Se permitió una operación fuera del rol.");
    }

    await auth.SalirAsync();
    Verificar(auth.Actual is null && !await auth.RequiereGestorAsync(), "Cerrar sesión elimina el actor.");
    await Denegar(() => solicitudes.ObtenerVistaCiudadanaAsync());
    try
    {
        await auth.AprovisionarAsync(new("Segunda gestión", "segundo-gestor@example.invalid", null), claveB);
        throw new InvalidOperationException("Se repitió el aprovisionamiento.");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("configuración inicial")) { }
    await auth.RegistrarAsync(new("Ciudadanía B", "  SEGUNDA@example.invalid ", null), claveB);
    Verificar(Autenticacion.Validar(new("  ", "correo inválido", "8095550100"), "corta", "distinta").Count == 5 &&
        await db.ExecuteScalarAsync<int>("SELECT COUNT(DISTINCT Salt) FROM Usuarios") ==
        await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Usuarios WHERE length(Hash) = 32 AND VersionDerivacion = 1"),
        "La validación comprueba los cinco campos y cada credencial tiene un salt distinto.");
    try
    {
        await auth.RegistrarAsync(new("Duplicada", correoB, null), claveB);
        throw new InvalidOperationException("Se aceptó un correo duplicado.");
    }
    catch (CorreoDuplicadoException) { }

    Verificar((await auth.EntrarAsync(correoB, claveA, true)).Identidad is null, "Contraseña incorrecta.");
    var (b, sinToken) = await auth.EntrarAsync(correoB, claveB, false);
    Verificar(b is { Rol: RolUsuario.Ciudadano } && sinToken is null, "Acceso ciudadano sin sesión persistida.");
    Verificar((await solicitudes.ObtenerVistaCiudadanaAsync()).Count == 0 &&
        (await solicitudes.ObtenerVistaCiudadanaAsync(idA)).Count == 0 &&
        (await solicitudes.ObtenerVistaCiudadanaAsync(idHistorico)).Count == 0 &&
        (await solicitudes.ObtenerCompletaAsync(idA)).Solicitud is null &&
        !(await solicitudes.ObtenerCompletaAsync(idA)).Equipos.Any() &&
        !await solicitudes.TieneAsignacionActivaAsync(idA),
        "Un ciudadano debe quedar aislado por propietario en listas, detalles, mapas y equipos.");
    var historica = await db.FindAsync<SolicitudRecoleccion>(idHistorico);
    Verificar(historica is { UsuarioId: null } && !await solicitudes.CompletarUbicacionAsync(historica,
        PuntoRecogida.Crear(18.5, -70)!), "No puede completar la ubicación histórica por ID.");
    await Denegar(() => solicitudes.ObtenerPorIdAsync(idA));
    await Denegar(() => solicitudes.EliminarAsync(idA));
    await Denegar(() => ResiduoElectronicoService.Instancia.ObtenerPorSolicitudIdAsync(idA));
    await Denegar(() => jornadas.ObtenerPanelAsync());
    await Denegar(() => jornadas.ObtenerMapaCampoAsync(1, idA));
    await Denegar(() => EstadisticasAmbientalesService.Instancia.ObtenerAsync());
    await Denegar(() => auth.CrearRecolectorAsync(new("Intento", correoCampo, null), claveCampo));

    var deB = new SolicitudRecoleccion { UsuarioId = -1, Zona = "Pantoja", DireccionReferencia = "Referencia de prueba",
        NombreContacto = "Contacto de prueba", TelefonoContacto = "+18095550100" };
    var equipoB = new ResiduoElectronico { Categoria = "Monitor", Descripcion = "Equipo de prueba", Cantidad = 1, PesoEstimado = 60 };
    Verificar(await solicitudes.GuardarSolicitudCompletaAsync(deB, [equipoB], [], null) && deB.UsuarioId == b!.Id,
        "El propietario lo asigna el servicio y no la vista.");
    Verificar(await solicitudes.CompletarUbicacionAsync(deB, PuntoRecogida.Crear(18.5, -70)!) &&
        (await solicitudes.ObtenerVistaCiudadanaAsync()).Single().Solicitud.Id == deB.Id,
        "El ciudadano completa su ubicación y ve su solicitud.");
    await Denegar(() => solicitudes.GuardarSolicitudCompletaAsync(deB, [equipoB], [], deB));
    await auth.SalirAsync();

    var (a, tokenA) = await auth.EntrarAsync(correoA, claveA, true);
    Verificar(a is { Rol: RolUsuario.Ciudadano } && tokenA is not null &&
        (await solicitudes.ObtenerVistaCiudadanaAsync()).Single().Solicitud.Id == idA &&
        (await solicitudes.ObtenerVistaCiudadanaAsync(deB.Id)).Count == 0, "Aislamiento recíproco.");
    Verificar(await new Autenticacion(database, TimeProvider.System).RestaurarAsync(tokenA) == a &&
        await new Autenticacion(database, new RelojFijo(DateTimeOffset.UtcNow.AddDays(15))).RestaurarAsync(tokenA) is null,
        "Restauración y vencimiento de la sesión.");
    Verificar(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Sesiones WHERE length(Huella) = 32") == 1,
        "SQLite guarda únicamente una huella.");
    await auth.SalirAsync();
    Verificar(await new Autenticacion(database, TimeProvider.System).RestaurarAsync(tokenA) is null,
        "El token revocado no vuelve a iniciar sesión.");

    await auth.EntrarAsync(correoGestor, claveGestor, false);
    Verificar((await solicitudes.ObtenerGestionAsync()).Any(f => f.Id == idHistorico) &&
        await solicitudes.ObtenerPorIdAsync(deB.Id) is not null, "Gestor conserva acceso a todos los registros.");
    await auth.CrearRecolectorAsync(new("Cuenta de campo", correoCampo, null), claveCampo);
    var cuentaCampo = (await auth.RecolectoresAsync()).Single();
    await auth.ActualizarRecolectorAsync(cuentaCampo.Id, new("Campo actualizado", correoCampo, null));
    var punto = PuntoRecogida.Crear(18.52, -70.02)!;
    var solicitudCampo = new SolicitudRecoleccion { Zona = "Herrera", DireccionReferencia = "Referencia operativa de prueba",
        Latitud = punto.Latitud, Longitud = punto.Longitud, PesoTotalEstimado = 500,
        NombreContacto = "Contacto de prueba", TelefonoContacto = "+18095550100" };
    await solicitudes.CrearAsync(solicitudCampo);
    var vista = await jornadas.ObtenerVistaPreviaAsync("Herrera");
    var programada = await jornadas.CrearAsync("Herrera", DateTime.Today, vista.Solicitudes.Select(s => s.Id).ToArray()) ??
        throw new InvalidOperationException("Falta jornada de campo para verificar permisos.");
    await jornadas.GenerarRutaAsync(programada.Id);
    await auth.SalirAsync();

    var (campo, tokenCampo) = await auth.EntrarAsync(correoCampo, claveCampo, true);
    Verificar(campo is { Rol: RolUsuario.Recolector, Nombre: "Campo actualizado" } && tokenCampo is not null &&
        (await jornadas.ObtenerPortadaCampoAsync()).Any(j => j.Id == programada.Id) &&
        await jornadas.ObtenerMapaCampoAsync(programada.Id, solicitudCampo.Id) is not null &&
        await jornadas.ObtenerMapaCampoAsync(programada.Id, int.MaxValue) is null &&
        (await jornadas.ObtenerConsultaCampoAsync(programada.Id, solicitudCampo.Id))?.Paradas
            .Single(p => p.Solicitud.Id == solicitudCampo.Id).Solicitud.TelefonoContacto == "+18095550100",
        "El recolector consulta únicamente las paradas vigentes y sus contactos.");
    await Denegar(() => jornadas.ObtenerDetalleAsync(programada.Id));
    await Denegar(() => jornadas.GenerarRutaAsync(programada.Id));
    await Denegar(() => jornadas.CancelarAsync(programada.Id));
    await Denegar(() => solicitudes.ObtenerVistaCiudadanaAsync(deB.Id));
    await Denegar(() => ResiduoElectronicoService.Instancia.ObtenerTodosAsync());

    // Un segundo coordinador de prueba representa otra sesión persistida de la misma instalación.
    var gestor = new Autenticacion(database, TimeProvider.System);
    await gestor.EntrarAsync(correoGestor, claveGestor, false);
    await gestor.EstadoRecolectorAsync(campo!.Id, false);
    var reapertura = new Autenticacion(database, TimeProvider.System);
    Verificar(await reapertura.RestaurarAsync(tokenCampo) is null &&
        (await reapertura.EntrarAsync(correoCampo, claveCampo, false)).Identidad is null, "Cuenta desactivada y token revocado.");
    await auth.SalirAsync();
    await gestor.EstadoRecolectorAsync(campo.Id, true);
    (_, tokenCampo) = await auth.EntrarAsync(correoCampo, claveCampo, true);
    await gestor.RestablecerRecolectorAsync(campo.Id, nuevaClave);
    Verificar(await reapertura.RestaurarAsync(tokenCampo) is null &&
        (await reapertura.EntrarAsync(correoCampo, claveCampo, false)).Identidad is null &&
        (await reapertura.EntrarAsync(correoCampo, nuevaClave, false)).Identidad?.Rol == RolUsuario.Recolector,
        "El restablecimiento revoca sesiones y sustituye la credencial.");
    await auth.SalirAsync();
    await auth.EntrarAsync(correoGestor, claveGestor, false);
    await jornadas.CancelarAsync(programada.Id);
    await auth.SalirAsync();
    await auth.EntrarAsync(correoCampo, nuevaClave, false);
    Verificar(await jornadas.ObtenerMapaCampoAsync(programada.Id, solicitudCampo.Id) is null &&
        await jornadas.ObtenerConsultaCampoAsync(programada.Id, solicitudCampo.Id) is null, "Una jornada cancelada no expone paradas.");
    await auth.SalirAsync();

    var reloj = new RelojFijo(DateTimeOffset.UtcNow);
    var intentos = new Autenticacion(database, reloj);
    for (int i = 0; i < 5; i++)
        Verificar((await intentos.EntrarAsync(correoB, claveA, false)).Identidad is null, "Intento rechazado.");
    Verificar((await intentos.EntrarAsync(correoB, claveB, false)).Identidad is null, "Pausa tras cinco intentos fallidos.");
    reloj.Avanzar(TimeSpan.FromSeconds(31));
    Verificar((await intentos.EntrarAsync(correoB, claveB, false)).Identidad is not null, "La pausa termina automáticamente.");

    string path = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "ecoruta-raee.db3");
    string legado = Path.Combine(Path.GetDirectoryName(path)!, "cuentas-v5.db3");
    await db.CloseAsync();
    File.Copy(path, legado);
    long secuenciaV5;
    int asociacionesV5;
    int paradasV5;
    int equiposV5;
    double umbralV5;
    using (var c = new SQLite.SQLiteConnection(legado))
    {
        QuitarEsquemaUsuarios(c);
        c.Execute("PRAGMA user_version=5");
        secuenciaV5 = c.ExecuteScalar<long>("SELECT UltimoNumero FROM SecuenciaCodigoSolicitud");
        asociacionesV5 = c.ExecuteScalar<int>("SELECT COUNT(*) FROM JornadaSolicitudes");
        paradasV5 = c.ExecuteScalar<int>("SELECT COUNT(OrdenRuta) FROM JornadaSolicitudes");
        equiposV5 = c.ExecuteScalar<int>("SELECT COUNT(*) FROM Residuos");
        umbralV5 = c.ExecuteScalar<double>("SELECT Peso FROM ConfiguracionJornadas WHERE Id = 1");
    }
    string fallo = Path.ChangeExtension(legado, ".fallo.db3");
    File.Copy(legado, fallo);
    using (var c = new SQLite.SQLiteConnection(fallo))
        c.Execute("CREATE INDEX IX_Solicitudes_UsuarioId ON Solicitudes (Id)");
    try
    {
        await new EcoRutaDatabase(fallo).GetConnectionAsync();
        throw new InvalidOperationException("Se esperaba fallo tardío de migración.");
    }
    catch (SQLite.SQLiteException) { }
    using (var c = new SQLite.SQLiteConnection(fallo))
        Verificar(c.ExecuteScalar<int>("PRAGMA user_version") == 5 &&
            c.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_schema WHERE name IN ('Usuarios','Sesiones')") == 0 &&
            c.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Solicitudes') WHERE name='UsuarioId'") == 0,
            "El fallo tardío debe revertir tablas, columna y versión.");
    var migrada = await new EcoRutaDatabase(legado).GetConnectionAsync();
    Verificar(await migrada.ExecuteScalarAsync<int>("PRAGMA user_version") == 6 &&
        await migrada.FindAsync<SolicitudRecoleccion>(deB.Id) is { UsuarioId: null, TelefonoContacto: "+18095550100" } &&
        await migrada.ExecuteScalarAsync<long>("SELECT UltimoNumero FROM SecuenciaCodigoSolicitud") == secuenciaV5 &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM JornadaSolicitudes") == asociacionesV5 &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(OrdenRuta) FROM JornadaSolicitudes") == paradasV5 &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Residuos") == equiposV5 &&
        await migrada.ExecuteScalarAsync<double>("SELECT Peso FROM ConfiguracionJornadas WHERE Id = 1") == umbralV5 &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_foreign_key_check") == 0 &&
        await migrada.ExecuteScalarAsync<string>("PRAGMA integrity_check") == "ok",
        "Migración v5 poblada, coordenadas, contacto, equipos y relaciones conservados.");
    Console.WriteLine("APROBADO: migración v6 y rollback, cuentas, aislamiento por ID y rol, restauración, caducidad, revocación y límite de intentos.");
}

static async Task VerificarMigracionContactoAsync()
{
    var directorio = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
    var original = Path.Combine(directorio, "ecoruta-raee.db3");
    var legado = Path.Combine(directorio, "contacto-v4-fixture.db3");
    File.Copy(original, legado);
    long secuencia;
    int asociaciones;
    int equipos;
    int ruta;
    string[] indices;
    using (var db = new SQLite.SQLiteConnection(legado))
    {
        QuitarEsquemaUsuarios(db);
        db.Execute("ALTER TABLE Solicitudes DROP COLUMN TelefonoContacto");
        db.Execute("ALTER TABLE Solicitudes DROP COLUMN NombreContacto");
        db.Execute("PRAGMA user_version=4");
        secuencia = db.ExecuteScalar<long>("SELECT UltimoNumero FROM SecuenciaCodigoSolicitud");
        asociaciones = db.ExecuteScalar<int>("SELECT COUNT(*) FROM JornadaSolicitudes");
        equipos = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Residuos");
        ruta = db.ExecuteScalar<int>("SELECT COUNT(OrdenRuta) FROM JornadaSolicitudes");
        indices = db.QueryScalars<string>("SELECT name FROM sqlite_schema WHERE type='index' ORDER BY name").ToArray();
    }
    var fallo = Path.Combine(directorio, "contacto-v4-fallo.db3");
    File.Copy(legado, fallo);
    using (var db = new SQLite.SQLiteConnection(fallo))
        db.Execute("ALTER TABLE Solicitudes ADD COLUMN TelefonoContacto TEXT");
    try
    {
        await new EcoRutaDatabase(fallo).GetConnectionAsync();
        throw new InvalidOperationException("La segunda escritura de la migración debió fallar.");
    }
    catch (SQLite.SQLiteException) { }
    using (var db = new SQLite.SQLiteConnection(fallo))
        Verificar(db.ExecuteScalar<int>("PRAGMA user_version") == 4 &&
            db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Solicitudes') WHERE name='NombreContacto'") == 0,
            "La primera columna y la versión deben revertirse si falla la segunda.");

    var migrada = await new EcoRutaDatabase(legado).GetConnectionAsync();
    Verificar(await migrada.ExecuteScalarAsync<int>("PRAGMA user_version") == 6 &&
        await migrada.ExecuteScalarAsync<long>("SELECT UltimoNumero FROM SecuenciaCodigoSolicitud") == secuencia &&
        asociaciones > 0 && await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM JornadaSolicitudes") == asociaciones &&
        equipos > 0 && await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Residuos") == equipos &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(OrdenRuta) FROM JornadaSolicitudes") == ruta &&
        (await migrada.QueryScalarsAsync<string>("SELECT name FROM sqlite_schema WHERE type='index' ORDER BY name")).Where(indices.Contains).SequenceEqual(indices) &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_foreign_key_check") == 0 &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Solicitudes WHERE NombreContacto IS NOT NULL OR TelefonoContacto IS NOT NULL") == 0,
        "La migración debe conservar datos, índices, secuencia y relaciones con contacto nulo.");
    Verificar(DatosContacto.TryNormalizarTelefono("(829) 555-0100", true, out var local) && local == "+18295550100" &&
        DatosContacto.TryNormalizarTelefono("849 555 0100", true, out _) &&
        DatosContacto.TryNormalizarTelefono("+34 600 000 000", false, out var internacional) && internacional == "+34600000000" &&
        !DatosContacto.TryNormalizarTelefono("8095550100", false, out _) &&
        !DatosContacto.TryNormalizarTelefono("2125550100", true, out _) &&
        !DatosContacto.TryNormalizarTelefono("+1 809 555 ABCD", false, out _),
        "La normalización exige prefijo explícito y formato compatible.");
    await migrada.CloseAsync();
    Console.WriteLine("APROBADO: migración v4 hasta v6 con datos, índices, claves foráneas, secuencia y rollback.");
}

static async Task VerificarZonasAsync()
{
    static void Zona(string json, string? esperada)
    {
        using var documento = JsonDocument.Parse(json);
        Verificar(BusquedaDirecciones.IdentificarZona(documento.RootElement) == esperada,
            $"Zona sugerida incorrecta. Esperada: {esperada ?? "manual"}");
    }

    Zona("""[{"country_code":"do","result_type":"street","suburb":"herrera"}]""", "Herrera");
    Zona("""[{"country_code":"do","result_type":"street","suburb":"Pantoja","city":"Los Alcarrizos"}]""", "Pantoja");
    Zona("""[{"country_code":"do","result_type":"district","name":"Distrito Municipal Pantoja","city":"Los Alcarrizos"}]""", "Pantoja");
    Zona("""[{"country_code":"do","result_type":"street","suburb":"Pantoja","district":"Los Alcarrizos"}]""", null);
    Zona("""[{"country_code":"do","result_type":"street","suburb":"Pantoja"},{"country_code":"do","result_type":"street","suburb":"Los Alcarrizos"}]""", null);
    Zona("""[{"country_code":"do","result_type":"street","city":"Los Alcarrizos"}]""", null);
    Zona("""[{"country_code":"do","result_type":"street","suburb":"Villa Duarte"}]""", null);
    Zona("""[{"country_code":"do","result_type":"county","name":"Pedro Brand"}]""", null);
    Zona("""[{"country_code":"do","result_type":"city","city":"Pedro Brand"}]""", null);
    Zona("[]", null);

    var punto = PuntoRecogida.Crear(18.5, -70)!;
    var vm = new SolicitudFormViewModel { DireccionReferencia = "Portón lateral" };
    vm.AgregarEquipoCommand.Execute(null);
    var equipo = vm.Equipos.Single();
    vm.AplicarUbicacion(SeleccionUbicacion.Crear(punto, "Pantoja", null)!);
    Verificar(vm.ZonaSeleccionada == "Pantoja" && vm.ZonaConfirmada &&
        vm.ObtenerPuntoRecogida() == punto && vm.DireccionReferencia == "Portón lateral" &&
        vm.Equipos.Single() == equipo, "La zona manual debe conservar referencia y equipos.");
    Verificar(SeleccionUbicacion.Crear(null, "Otra zona", " ") is null &&
        SeleccionUbicacion.Crear(null, "Otra zona", " Sector local ")?.Sector == "Sector local",
        "Otra zona debe exigir sector incluso sin coordenadas.");

    int llamadas = 0;
    using var http = new HttpClient(new MapaHttp((request, _) =>
    {
        llamadas++;
        Verificar(request.RequestUri!.AbsolutePath.EndsWith("/reverse") &&
            !request.RequestUri.Query.Contains("Portón", StringComparison.Ordinal), "La consulta inversa solo debe incluir el punto.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
    }));
    var proveedor = new BusquedaDirecciones(http, TimeProvider.System);
    Verificar((await proveedor.SugerirZonaAsync(punto, null, default)).Estado == EstadoBusqueda.SinClave,
        "Sin clave no debe consultar.");
    Verificar((await proveedor.SugerirZonaAsync(punto, "fixture", default)).Estado == EstadoBusqueda.Limite &&
        (await proveedor.BuscarAsync("parque", "fixture", default)).Estado == EstadoBusqueda.Limite && llamadas == 1,
        "Debe compartir la pausa por cuota entre búsquedas.");

    using var sinRed = new HttpClient(new MapaHttp((_, _) => throw new HttpRequestException()));
    Verificar((await new BusquedaDirecciones(sinRed, TimeProvider.System)
        .SugerirZonaAsync(punto, "fixture", default)).Estado == EstadoBusqueda.NoDisponible, "Error de red.");

    using var cancelar = new CancellationTokenSource();
    using var lenta = new HttpClient(new MapaHttp(async (_, token) =>
    {
        await Task.Delay(Timeout.Infinite, token);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }));
    var pendiente = new BusquedaDirecciones(lenta, TimeProvider.System)
        .SugerirZonaAsync(punto, "fixture", cancelar.Token);
    cancelar.Cancel();
    try { await pendiente; throw new InvalidOperationException("La consulta debía cancelarse."); }
    catch (OperationCanceledException) { }
    Console.WriteLine("APROBADO: zonas explícitas y ambiguas, borrador manual, red, cuota compartida y cancelación.");
}

sealed class MapaHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        responder(request, cancellationToken);
}

internal sealed class RelojFijo(DateTimeOffset instante) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instante;
    internal void Avanzar(TimeSpan lapso) => instante = instante.Add(lapso);
}
