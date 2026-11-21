using System.Diagnostics;
using SQLite;

namespace ECORUTA_RAEE.ViewModels;

internal static class OperacionBaseDatos
{
    public static async Task<bool> EjecutarAsync(Func<Task> operacion)
    {
        try
        {
            await operacion();
            return true;
        }
        catch (SQLiteException ex) when (((int)ex.Result & 0xff) == (int)SQLite3.Result.Constraint)
        {
            Debug.WriteLine(ex);
            if (Shell.Current is { } shell)
                await shell.DisplayAlertAsync(
                    "Restricción de datos",
                    "La operación entra en conflicto con los datos registrados. Comprueba que los registros relacionados todavía existan.",
                    "Aceptar");
        }
        catch (Exception ex) when (ex is SQLiteException or IOException or UnauthorizedAccessException
            or InvalidOperationException or TypeInitializationException or DllNotFoundException)
        {
            Debug.WriteLine(ex);
            if (Shell.Current is { } shell)
                await shell.DisplayAlertAsync(
                    "Error de base de datos",
                    "No se pudo completar la operación. Vuelve a intentarlo.",
                    "Aceptar");
        }

        return false;
    }
}
