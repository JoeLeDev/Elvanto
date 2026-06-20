using System;
using System.Windows.Media;

namespace ElvantoKiosk.Services;

public static class ColorHelper
{
    public static bool TryParseHex(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var hex = value.Trim();
        if (hex.StartsWith('#'))
            hex = hex[1..];

        if (hex.Length is 6 or 8)
        {
            try
            {
                color = (Color)ColorConverter.ConvertFromString("#" + hex)!;
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public static Color ParseHexOrDefault(string? value, Color fallback)
    {
        return TryParseHex(value, out var color) ? color : fallback;
    }
}
