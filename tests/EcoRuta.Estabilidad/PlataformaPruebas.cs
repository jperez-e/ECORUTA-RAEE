namespace Microsoft.Maui.Storage
{
    internal static class FileSystem
    {
        public static string AppDataDirectory { get; } =
            Directory.CreateTempSubdirectory("ecoruta-estabilidad-").FullName;
    }
}

internal sealed class Shell
{
    public static Shell Current { get; } = new();
    public Task DisplayAlertAsync(string titulo, string texto, string aceptar) => Task.CompletedTask;
    public Task<bool> DisplayAlertAsync(string titulo, string texto, string aceptar, string cancelar) =>
        Task.FromResult(true);
    public string? UltimaRuta { get; private set; }
    public IDictionary<string, object>? UltimosParametros { get; private set; }
    public Task GoToAsync(string ruta, IDictionary<string, object>? parametros = null)
    {
        UltimaRuta = ruta;
        UltimosParametros = parametros;
        return Task.CompletedTask;
    }
}

internal sealed class ShellNavigationQueryParameters : Dictionary<string, object> { }

namespace ECORUTA_RAEE.Views
{
    internal sealed class SolicitudFormPage { }
    internal sealed class ResiduosSolicitudPage { }
}
