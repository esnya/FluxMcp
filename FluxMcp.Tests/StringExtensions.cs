namespace FluxMcp.Tests;

public static class StringExtensions
{
    public static string UnescapeUnicodeCharacters(this string input)
    {
        return System.Text.RegularExpressions.Regex.Unescape(input);
    }
}
