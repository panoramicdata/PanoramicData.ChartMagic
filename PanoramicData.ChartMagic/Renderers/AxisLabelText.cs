namespace PanoramicData.ChartMagic.Renderers;

internal static class AxisLabelText
{
	/// <summary>The label an X tick has before any is thinned or shortened.</summary>
	internal static string XAxisLabel(Chart chart, PlotGeometry geometry, double value)
		=> geometry.IsHorizontalPlot
			? FormatAxisValue(value, chart.ChartArea.XAxis, chart.Culture)
			: geometry.CategoryLabel(value) ?? FormatAxisValue(value, chart.ChartArea.XAxis, chart.Culture);

	/// <summary>The label a Y tick has before any is thinned or shortened.</summary>
	internal static string YAxisLabel(Chart chart, PlotGeometry geometry, double value)
		=> geometry.IsHorizontalPlot
			? geometry.CategoryLabel(value) ?? FormatAxisValue(value, chart.ChartArea.YAxis, chart.Culture)
			: FormatAxisValue(value, chart.ChartArea.YAxis, chart.Culture);

	/// <summary>
	/// Formats an axis value, honouring an explicit format string and the short-label option.
	/// </summary>
	internal static string FormatAxisValue(double value, AxisArea axis, CultureInfo culture)
	{
		if (axis.LabelFormat is { Length: > 0 })
		{
			return value.ToString(axis.LabelFormat, culture);
		}

		if (axis.UseShortLabels)
		{
			return ShortAxisLabel(value, culture);
		}

		// Two decimal places at most, and none where the value does not need them.
		return value.ToString("0.##", culture);
	}

	/// <summary>
	/// A short axis label: one decimal place, with a suffix once the value reaches a thousand.
	/// </summary>
	/// <remarks>
	/// Measured against DocMagic: with short labels on and values topping out at 35, its axis
	/// reads 35.0, 30.0, 25.0 rather than 35, 30, 25. So "short" is not only about abbreviating
	/// large numbers - it is a fixed one-decimal format throughout, and this implementation left
	/// anything under a thousand alone, which is why the setting had no effect on a percentage
	/// axis.
	/// </remarks>
	internal static string ShortAxisLabel(double value, CultureInfo culture)
	{
		var absolute = Math.Abs(value);
		if (absolute >= 1_000_000_000)
		{
			return (value / 1_000_000_000).ToString("0.0", culture) + "G";
		}

		if (absolute >= 1_000_000)
		{
			return (value / 1_000_000).ToString("0.0", culture) + "M";
		}

		if (absolute >= 1_000)
		{
			return (value / 1_000).ToString("0.0", culture) + "K";
		}

		return value.ToString("0.0", culture);
	}
}
