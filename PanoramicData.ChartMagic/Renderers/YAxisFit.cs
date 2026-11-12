using Canvas = PanoramicData.ChartMagic.Renderers.AxisLayout.Canvas;
using static PanoramicData.ChartMagic.Renderers.AxisLabelGeometry;
using static PanoramicData.ChartMagic.Renderers.AxisLabelFit;

namespace PanoramicData.ChartMagic.Renderers;

internal static class YAxisFit
{
	internal static List<string?> Settle(Chart chart, Canvas canvas)
	{
		var yAxis = chart.ChartArea.YAxis;
		var innerPlot = chart.ChartArea.InnerPlot;
		var geometry = canvas.Geometry();
		var labels = AxisTicks.YAxisTickValues(chart, geometry)
			.Select(value => (string?)AxisLabelText.YAxisLabel(chart, geometry, value))
			.ToList();

		if (!IsFittable(yAxis, Math.Abs(yAxis.LabelAngle) <= 90, labels.Count, yAxis.XPositionPercent + yAxis.WidthPercent, innerPlot.XPositionPercent))
		{
			return labels;
		}

		var stripWidth = canvas.Width * yAxis.GetCanvasWidthPercent() / 100;
		var labelsReach = yAxis.TickLengthPixels + AxisRenderer.TickLabelGapPixels + HorizontalReach(Widest(labels, yAxis), yAxis.LabelAngle, yAxis.FontSize);
		if (!YAxisNeedsRoom(yAxis, stripWidth, labelsReach))
		{
			return labels;
		}

		var titleRoom = TitleRoom(yAxis, yAxis.FontSize + AxisRenderer.YAxisTitleClearancePixels);
		var target = GrowYAxisMargin(chart, canvas, innerPlot.GetCanvasWidthPercent(), stripWidth, labelsReach + titleRoom);
		return YAxisStillFits(labelsReach, titleRoom, target) || !geometry.IsHorizontalPlot
			? labels
			: ShortenYLabels(yAxis, labels, target, titleRoom);
	}

	internal static double GrowYAxisMargin(Chart chart, Canvas canvas, double innerPlotPercent, double stripWidth, double needed)
	{
		var chartAreaWidth = canvas.Width * chart.ChartArea.GetCanvasWidthPercent() / 100;
		var target = Target(stripWidth, needed, chartAreaWidth, canvas.Width * innerPlotPercent / 100);
		GrowLeftMargin(chart, (target - stripWidth) / chartAreaWidth * 100);
		return target;
	}

	internal static bool YAxisStillFits(double labelsReach, double titleRoom, double target) => labelsReach + titleRoom <= target;

	/// <summary>Whether a Y label is cut off, or the title would be too small to read.</summary>
	internal static bool YAxisNeedsRoom(AxisArea yAxis, double stripWidth, double labelsReach)
		=> labelsReach + EdgePaddingPixels > stripWidth
			|| (HasTitle(yAxis) && stripWidth - labelsReach - AxisRenderer.YAxisTitleClearancePixels < AxisRenderer.MinimumTitleFontSize);

	internal static void GrowLeftMargin(Chart chart, double growthPercent)
	{
		if (growthPercent <= 0)
		{
			return;
		}

		chart.ChartArea.InnerPlot.XPositionPercent += growthPercent;
		chart.ChartArea.InnerPlot.WidthPercent -= growthPercent;
		chart.ChartArea.YAxis.WidthPercent += growthPercent;
	}

	/// <summary>
	/// Category labels shortened to the capped strip, keeping room for the title only while that
	/// leaves them a useful length.
	/// </summary>
	internal static List<string?> ShortenYLabels(AxisArea yAxis, List<string?> labels, double stripWidth, double titleRoom)
	{
		var labelRoom = stripWidth - yAxis.TickLengthPixels - AxisRenderer.TickLabelGapPixels;
		var maximumWidth = labelRoom - titleRoom >= 3 * yAxis.FontSize ? labelRoom - titleRoom : labelRoom - EdgePaddingPixels;
		var fitting = WidthWithinReach(maximumWidth, yAxis.LabelAngle, yAxis.FontSize);
		return [.. labels.Select(label => Fit(label, fitting, yAxis))];
	}

	/// <summary>
	/// Every Nth Y label, where the labels are closer together than a line of text.
	/// </summary>
	internal static List<string?> ThinToFit(Chart chart, Canvas canvas, List<string?> labels)
	{
		var yAxis = chart.ChartArea.YAxis;
		if (!yAxis.IsEnabled || !yAxis.LabelsEnabled || Math.Abs(yAxis.LabelAngle) > 90 || labels.Count < 2)
		{
			return labels;
		}

		var geometry = canvas.Geometry();
		var positions = AxisTicks.YAxisTickValues(chart, geometry)
			.Select(value => geometry.IsHorizontalPlot ? geometry.CategoryToPixels(value) : geometry.YToPixels(value))
			.ToList();
		Thin(labels, positions, yAxis.FontSize + 1, Math.Max(Math.Abs(Math.Cos(yAxis.LabelAngle * Math.PI / 180)), MinimumCosine));
		return labels;
	}
}
