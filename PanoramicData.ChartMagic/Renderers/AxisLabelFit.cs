using static PanoramicData.ChartMagic.Renderers.AxisLabelGeometry;

namespace PanoramicData.ChartMagic.Renderers;

internal static class AxisLabelFit
{
	/// <summary>The most of the chart area an axis strip may grow to take.</summary>
	internal const double MaximumStripFraction = 0.5;

	/// <summary>The space kept between neighbouring labels, and between a label and a title.</summary>
	internal const double LabelSpacingPixels = 4;

	/// <summary>The most of the plot's own width or height a margin may grow into.</summary>
	internal const double MaximumPlotShare = 0.5;

	/// <summary>Whether an axis's labels are this layout's to fit: drawn, at an angle it can fit, and in a strip that meets the plot.</summary>
	internal static bool IsFittable(AxisArea axis, bool angleFits, int labelCount, double stripEdgePercent, double plotEdgePercent)
		=> axis.IsEnabled
			&& axis.LabelsEnabled
			&& angleFits
			&& labelCount > 0
			&& Math.Abs(stripEdgePercent - plotEdgePercent) < 0.01;

	internal static bool HasTitle(AxisArea axis) => axis.Title is { Length: > 0 };

	internal static double TitleRoom(AxisArea axis, double roomWithTitle) => HasTitle(axis) ? roomWithTitle : EdgePaddingPixels;

	/// <summary>
	/// The size a strip grows to for what it needs, never past half of the chart area or half of the
	/// plot, and never smaller than it is.
	/// </summary>
	internal static double Target(double strip, double needed, double chartArea, double plot)
		=> Math.Min(needed, Math.Max(strip, Math.Min(chartArea * MaximumStripFraction, strip + (plot * MaximumPlotShare))));

	/// <summary>Whether any two neighbouring labels, drawn level, would overlap.</summary>
	internal static bool Overlaps(List<double> positions, List<double> widths)
		=> Enumerable.Range(1, positions.Count - 1)
			.Any(i => Math.Abs(positions[i] - positions[i - 1]) < ((widths[i] + widths[i - 1]) / 2) + LabelSpacingPixels);

	/// <summary>
	/// Blanks all but every Nth label, N being the smallest step that puts a line of text between
	/// neighbouring labels.
	/// </summary>
	/// <param name="labels">The labels, thinned in place.</param>
	/// <param name="positions">Where each label sits along its axis.</param>
	/// <param name="lineHeight">The spacing a label needs from the next.</param>
	/// <param name="spacingFactor">The share of the spacing along the axis that is across the labels.</param>
	internal static void Thin(List<string?> labels, List<double> positions, double lineHeight, double spacingFactor)
	{
		if (positions.Count < 2 || positions.Count != labels.Count)
		{
			return;
		}

		var spacing = Enumerable.Range(1, positions.Count - 1).Min(i => Math.Abs(positions[i] - positions[i - 1])) * spacingFactor;
		if (spacing >= lineHeight)
		{
			return;
		}

		var step = spacing <= 0 ? labels.Count : (int)Math.Ceiling(lineHeight / spacing);
		for (var i = 0; i < labels.Count; i++)
		{
			if (i % step != 0)
			{
				labels[i] = null;
			}
		}
	}
}
