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
