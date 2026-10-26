using System.Text.RegularExpressions;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Turns a series' label text into the label for one point, replacing the Microsoft chart
/// keywords that appear in report templates.
/// </summary>
/// <remarks>
/// Shared by pies, funnels and every series drawn against axes.
/// </remarks>
internal static partial class DataLabelText
{
	/// <summary>
	/// A keyword, with the .NET format string that may follow it in braces: #PERCENT{P0}.
	/// </summary>
	/// <remarks>
	/// The longer keywords come first in the alternation, because #VAL and #SER are prefixes of
	/// others. One pass, so a substituted value is never searched for keywords itself.
	/// </remarks>
	[GeneratedRegex(
		@"#(?<keyword>VALX|VALY|VAL|PERCENT|TOTAL|LEGENDTEXT|SERIESNAME|SER)(?:\{(?<format>[^{}]*)\})?",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex KeywordPattern();

	/// <summary>
	/// The label for a point, or null when the series has no label text.
	/// </summary>
	/// <param name="text">The series' label text.</param>
	/// <param name="point">The point being labelled.</param>
	/// <param name="value">The value drawn for the point.</param>
	/// <param name="seriesName">The name of the series the point belongs to.</param>
	/// <param name="percentage">The point's share of the series total, 0 to 100.</param>
	/// <param name="total">The series total.</param>
	/// <param name="culture">The culture numbers are written in; invariant when null.</param>
	/// <remarks>
	/// An unrecognised keyword is left in place rather than blanked, so it shows on the chart as
	/// itself instead of vanishing. A format the value cannot take falls back to the unformatted
	/// value. A format after a text keyword is not a number format, so it is left as written.
	/// </remarks>
	internal static string? Substitute(
		string? text,
		ChartPoint point,
		double value,
		string? seriesName,
		double percentage,
		double total,
		CultureInfo? culture = null)
	{
		if (text is not { Length: > 0 })
		{
			return null;
		}

		culture ??= CultureInfo.InvariantCulture;

		return KeywordPattern().Replace(text, match =>
		{
			var format = match.Groups["format"] is { Success: true, Length: > 0 } group ? group.Value : null;
			return match.Groups["keyword"].Value.ToUpperInvariant() switch
			{
				"VALX" => point.XValueString ?? FormatNumber(point.XValue, format, culture),
				"VALY" or "VAL" => FormatNumber(value, format, culture),
				// The Microsoft chart control formats the share as a fraction, so P0 gives "33%".
				"PERCENT" => (format is null ? null : Formatted(percentage / 100, format, culture))
					?? percentage.ToString("0.00", culture) + "%",
				"TOTAL" => FormatNumber(total, format, culture),
				"LEGENDTEXT" => (point.LegendText ?? string.Empty) + Braced(format),
				_ => (seriesName ?? string.Empty) + Braced(format)
			};
		});
	}

	/// <summary>
	/// A number as a label shows it: up to two decimal places, and none when it is whole.
	/// </summary>
	internal static string FormatNumber(double value, CultureInfo culture) => value.ToString("0.##", culture);

	private static string FormatNumber(double value, string? format, CultureInfo culture)
		=> (format is null ? null : Formatted(value, format, culture)) ?? FormatNumber(value, culture);

	private static string? Formatted(double value, string format, CultureInfo culture)
	{
		try
		{
			return value.ToString(format, culture);
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private static string Braced(string? format) => format is null ? string.Empty : "{" + format + "}";
}
