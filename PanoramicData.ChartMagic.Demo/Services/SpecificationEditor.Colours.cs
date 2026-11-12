using System.Drawing;
using System.Globalization;

namespace PanoramicData.ChartMagic.Demo.Services;

public static partial class SpecificationEditor
{
	/// <summary>
	/// A colour in CSS terms, which is what the colour picker reads and writes: transparent,
	/// a six-digit hex, or rgba where there is partial alpha.
	/// </summary>
	private static string FormatColour(Color colour) => colour.A switch
	{
		0 => "transparent",
		255 => FormattableString.Invariant($"#{colour.R:X2}{colour.G:X2}{colour.B:X2}"),
		_ => FormattableString.Invariant(
			$"rgba({colour.R}, {colour.G}, {colour.B}, {colour.A / 255.0:0.###})")
	};

	/// <summary>
	/// Parses what the colour picker and a person are both likely to type: transparent, a
	/// three, six or eight digit hex, an rgb or rgba function, or a named colour.
	/// </summary>
	/// <remarks>
	/// Eight-digit hex is read as RRGGBBAA, the CSS order, because that is what the picker
	/// emits. Reading it as AARRGGBB would silently swap the alpha for the red channel.
	/// </remarks>
	private static bool TryParseColour(string text, out object? parsed)
	{
		if (string.Equals(text, "transparent", StringComparison.OrdinalIgnoreCase))
		{
			parsed = Color.FromArgb(0, 0, 0, 0);
			return true;
		}

		if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
		{
			return TryParseRgb(text, out parsed);
		}

		if (text.StartsWith('#'))
		{
			return TryParseHex(text[1..], out parsed);
		}

		var named = Color.FromName(text);
		parsed = named.IsKnownColor ? named : null;
		return named.IsKnownColor;
	}

	private static bool TryParseRgb(string text, out object? parsed)
	{
		parsed = null;
		var inside = text[(text.IndexOf('(', StringComparison.Ordinal) + 1)..].TrimEnd(')');
		var parts = inside.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length is < 3 or > 4
			|| !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var red)
			|| !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var green)
			|| !int.TryParse(parts[2], CultureInfo.InvariantCulture, out var blue))
		{
			return false;
		}

		var alpha = 1.0;
		if (parts.Length == 4
			&& !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out alpha))
		{
			return false;
		}

		parsed = Color.FromArgb(
			(int)Math.Round(Math.Clamp(alpha, 0, 1) * 255),
			Math.Clamp(red, 0, 255),
			Math.Clamp(green, 0, 255),
			Math.Clamp(blue, 0, 255));
		return true;
	}

	private static bool TryParseHex(string hex, out object? parsed)
	{
		if (hex.Length == 3)
		{
			hex = string.Concat(hex.Select(character => new string(character, 2)));
		}

		if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
		{
			parsed = null;
			return false;
		}

		parsed = hex.Length switch
		{
			6 => Color.FromArgb(255, (int)((value >> 16) & 0xFF), (int)((value >> 8) & 0xFF), (int)(value & 0xFF)),
			8 => Color.FromArgb((int)(value & 0xFF), (int)((value >> 24) & 0xFF), (int)((value >> 16) & 0xFF), (int)((value >> 8) & 0xFF)),
			_ => null
		};
		return parsed is not null;
	}
}
