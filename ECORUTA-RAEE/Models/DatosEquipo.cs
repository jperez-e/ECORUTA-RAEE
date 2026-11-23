using System.Collections.ObjectModel;
using System.Globalization;

namespace ECORUTA_RAEE.Models;

internal static class DatosEquipo
{
    internal static ReadOnlyCollection<string> Categorias { get; } =
        Array.AsReadOnly(new[] { "Computadora", "Monitor", "Celular", "Impresora", "Tablet", "Televisor", "Otro" });

    internal static string ErrorCategoria(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "Selecciona una categoría." : string.Empty;

    internal static string ErrorDescripcion(string valor) =>
        valor.Length is < 5 or > 150 ? "La descripción debe tener entre 5 y 150 caracteres." : string.Empty;

    internal static string ErrorCantidad(int valor) =>
        valor is < 1 or > 1000 ? "Introduce una cantidad entre 1 y 1000." : string.Empty;

    internal static string ErrorPeso(double valor) =>
        !double.IsFinite(valor) || valor <= 0 || valor > 10000
            ? "Introduce un peso mayor que 0 y hasta 10000 kg." : string.Empty;

    internal static int LeerCantidad(string? texto) =>
        int.TryParse(texto?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int valor) ? valor : 0;

    internal static double LeerPeso(string? texto) =>
        double.TryParse(texto?.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture,
            out double valor) ? valor : double.NaN;

    internal static bool EsValido(ResiduoElectronico equipo) =>
        ErrorCategoria(equipo.Categoria).Length == 0 &&
        ErrorDescripcion(equipo.Descripcion?.Trim() ?? string.Empty).Length == 0 &&
        ErrorCantidad(equipo.Cantidad).Length == 0 &&
        ErrorPeso(equipo.PesoEstimado).Length == 0;
}
