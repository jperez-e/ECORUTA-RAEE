namespace ECORUTA_RAEE.Models;

internal static class DatosContacto
{
    internal const string Internacional = "Internacional (+código de país)";
    internal const string Dominicano = "República Dominicana (+1)";

    internal static string? NormalizarNombre(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();

    internal static bool NombreValido(string? nombre) =>
        nombre is null || (nombre.Length <= 100 && !nombre.Any(char.IsControl) && nombre.Any(char.IsLetter));

    internal static bool TryNormalizarTelefono(string? texto, bool dominicano, out string? telefono)
    {
        telefono = null;
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        string compacto = string.Concat(texto.Trim().Where(c => c is not (' ' or '-' or '(' or ')' or '.')));
        if (dominicano && !compacto.StartsWith('+'))
            compacto = "+1" + compacto;
        if (compacto.Length is < 9 or > 16 || compacto[0] != '+' ||
            compacto[1] == '0' || !compacto.AsSpan(1).ContainsOnlyAsciiDigits())
            return false;

        if (compacto.StartsWith("+1", StringComparison.Ordinal))
        {
            if (compacto.Length != 12 || compacto[2] < '2' || compacto[5] < '2')
                return false;
        }
        if (dominicano && compacto[..5] is not ("+1809" or "+1829" or "+1849"))
            return false;

        telefono = compacto;
        return true;
    }

    private static bool ContainsOnlyAsciiDigits(this ReadOnlySpan<char> texto)
    {
        foreach (char c in texto)
            if (!char.IsAsciiDigit(c))
                return false;
        return true;
    }
}
