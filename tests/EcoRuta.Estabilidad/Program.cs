using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using System.Net;
using System.Text.Json;

await VerificarZonasAsync();

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
    await db.ExecuteScalarAsync<int>("PRAGMA user_version") == 5,
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
    await reabierta.ExecuteScalarAsync<int>("PRAGMA user_version") == 5,
    "Los puntos corregidos y los registros históricos deben mantenerse tras reabrir SQLite.");
Console.WriteLine("APROBADO: alta integrada sin punto y persistencia tras reabrir SQLite v5.");

Verificar(await reabierta.FindAsync<SolicitudRecoleccion>(primera.Id) is { TelefonoContacto: "+18095550100" },
    "El contacto se conserva al reabrir.");
await reabierta.CloseAsync();
await VerificarMigracionContactoAsync();
var publicada = (await servicio.ObtenerVistaCiudadanaAsync(primera.Id)).Single();
Verificar(publicada.FechaProgramada == DateTime.Today && publicada.EstadoVisual == "Programada",
    "La vista ciudadana debe consultar la fecha de la jornada activa.");
Verificar(await jornadas.CancelarAsync(programada.Id), "Se debe poder cancelar la jornada de prueba.");
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
    Verificar(await migrada.ExecuteScalarAsync<int>("PRAGMA user_version") == 5 &&
        await migrada.ExecuteScalarAsync<long>("SELECT UltimoNumero FROM SecuenciaCodigoSolicitud") == secuencia &&
        asociaciones > 0 && await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM JornadaSolicitudes") == asociaciones &&
        equipos > 0 && await migrada.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Residuos") == equipos &&
        await migrada.ExecuteScalarAsync<int>("SELECT COUNT(OrdenRuta) FROM JornadaSolicitudes") == ruta &&
        (await migrada.QueryScalarsAsync<string>("SELECT name FROM sqlite_schema WHERE type='index' ORDER BY name")).SequenceEqual(indices) &&
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
    Console.WriteLine("APROBADO: migración v4 a v5 con datos, índices, claves foráneas, secuencia y rollback.");
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
