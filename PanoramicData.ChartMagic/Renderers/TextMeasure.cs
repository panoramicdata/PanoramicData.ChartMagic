using SkiaSharp;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// How wide a piece of text is when drawn in the font this library renders with.
/// </summary>
/// <remarks>
/// Measured with the embedded face, which is what every raster render draws with, so a layout
/// decided here matches the pixels. A character-count estimate cannot tell "Manchester" from
/// "Illinois", which is the difference between a legend entry fitting and being cut off.
/// </remarks>
internal static class TextMeasure
{
	/// <summary>
	/// Synthetic bold is drawn wider than the regular face it is made from.
	/// </summary>
	private const double BoldWidening = 1.06;

	/// <summary>
	/// The width per character, as a fraction of the size, if the embedded face cannot be loaded.
	/// </summary>
	private const double FallbackAverageWidth = 0.55;

	internal static double Width(string text, double fontSize, FontWeight fontWeight = FontWeight.Normal)
	{
		if (string.IsNullOrEmpty(text) || fontSize <= 0)
		{
			return 0;
		}

		var widening = fontWeight == FontWeight.Bold ? BoldWidening : 1;
		var typeface = EmbeddedTypefaceProvider.Default;
		if (typeface is null)
		{
			return text.Length * fontSize * FallbackAverageWidth * widening;
		}

		using var font = new SKFont(typeface, (float)fontSize);
		return font.MeasureText(text) * widening;
	}

	/// <summary>
	/// The text, shortened with an ellipsis until it fits the width; the text itself when it fits.
	/// </summary>
	/// <remarks>
	/// An ellipsis rather than a hard cut, so a reader can tell the label was shortened rather than
	/// reading "Memor" as the name of the series.
	/// </remarks>
	internal static string Fit(string text, double maximumWidth, double fontSize, FontWeight fontWeight = FontWeight.Normal)
	{
		if (Width(text, fontSize, fontWeight) <= maximumWidth)
		{
			return text;
		}

		const string ellipsis = "...";
		for (var length = text.Length - 1; length > 0; length--)
		{
			var candidate = text[..length].TrimEnd() + ellipsis;
			if (Width(candidate, fontSize, fontWeight) <= maximumWidth)
			{
				return candidate;
			}
		}

		return ellipsis;
	}

	/// <summary>
	/// Breaks text into lines of at most the threshold's length, at spaces where it can.
	/// </summary>
	/// <remarks>
	/// The Microsoft chart control's legend TextWrapThreshold: a count of characters, not a width.
	/// A word longer than the threshold is kept whole on its own line, as that control does.
	/// </remarks>
	internal static IReadOnlyList<string> Wrap(string text, int threshold)
	{
		if (threshold <= 0 || text.Length <= threshold)
		{
			return [text];
		}

		var lines = new List<string>();
		var line = new StringBuilder();
		foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			if (line.Length > 0 && line.Length + 1 + word.Length > threshold)
			{
				lines.Add(line.ToString());
				line.Clear();
			}

			if (line.Length > 0)
			{
				line.Append(' ');
			}

			line.Append(word);
		}

		if (line.Length > 0)
		{
			lines.Add(line.ToString());
		}

		return lines;
	}
}
