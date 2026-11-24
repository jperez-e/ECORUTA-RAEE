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
    public Task GoToAsync(string ruta) => Task.CompletedTask;
}
