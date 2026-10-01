namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Turns a series' label text into the label for one point, replacing the Microsoft chart
/// keywords that appear in report templates.
/// </summary>
/// <remarks>
/// This used to live in the pie renderer and serve pies alone, so label text on any other chart
/// type drew nothing at all. It is shared now by pies, funnels and every series drawn against
/// axes.
/// </remarks>
internal static class DataLabelText
{
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
	/// The keyword set is deliberately small: these are what appears in practice. An unrecognised
	/// keyword is left in place rather than blanked, so that it shows up as itself on the chart
	/// instead of vanishing silently.
	///
	/// #VAL is the Microsoft chart shorthand for #VALY. It is replaced after #VALX and #VALY,
	/// because it is a prefix of both.
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
		var formattedValue = FormatNumber(value, culture);

		return text
			.Replace("#VALX", point.XValueString ?? FormatNumber(point.XValue, culture), StringComparison.OrdinalIgnoreCase)
			.Replace("#VALY", formattedValue, StringComparison.OrdinalIgnoreCase)
			.Replace("#VAL", formattedValue, StringComparison.OrdinalIgnoreCase)
			.Replace("#PERCENT", percentage.ToString("0.00", culture) + "%", StringComparison.OrdinalIgnoreCase)
			.Replace("#TOTAL", FormatNumber(total, culture), StringComparison.OrdinalIgnoreCase)
			.Replace("#LEGENDTEXT", point.LegendText ?? string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("#SERIESNAME", seriesName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("#SER", seriesName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// A number as a label shows it: up to two decimal places, and none when it is whole.
	/// </summary>
	internal static string FormatNumber(double value, CultureInfo culture) => value.ToString("0.##", culture);
}
