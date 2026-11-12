using Canvas = PanoramicData.ChartMagic.Renderers.AxisLayout.Canvas;
using static PanoramicData.ChartMagic.Renderers.AxisLabelGeometry;
using static PanoramicData.ChartMagic.Renderers.AxisLabelFit;

namespace PanoramicData.ChartMagic.Renderers;

internal static class XAxisFit
{
	internal static (int Angle, List<string?> Labels) Settle(Chart chart, Canvas canvas)
	{
		var xAxis = chart.ChartArea.XAxis;
		var innerPlot = chart.ChartArea.InnerPlot;
		var geometry = canvas.Geometry();
		var values = AxisTicks.XAxisTickValues(chart, geometry);
		var labels = values.Select(value => (string?)AxisLabelText.XAxisLabel(chart, geometry, value)).ToList();

		if (!IsFittable(xAxis, xAxis.LabelAngle is >= -90 and <= 0, labels.Count, xAxis.YPositionPercent + xAxis.HeightPercent, innerPlot.YPositionPercent))
		{
			return (xAxis.LabelAngle, labels);
		}

		var positions = values.Select(value => AxisTicks.XAxisPixels(geometry, value)).ToList();
		var widths = labels.Select(label => TextMeasure.Width(label!, xAxis.FontSize, xAxis.FontWeight)).ToList();
		var angle = xAxis.LabelAngle != 0 ? xAxis.LabelAngle : Overlaps(positions, widths) ? AxisLayout.RotatedLabelAngle : 0;
		if (angle != 0)
		{
			Thin(labels, positions, xAxis.FontSize + 1, Sine(angle));
		}

		var labelTop = xAxis.TickLengthPixels + AxisRenderer.TickLabelGapPixels;
		var titleRoom = TitleRoom(xAxis, xAxis.FontSize + LabelSpacingPixels);
		var needed = labelTop + LabelsDepth(labels, widths, xAxis.FontSize, angle) + titleRoom;
		var target = GrowXAxisMargin(chart, canvas, innerPlot.GetCanvasHeightPercent(), xAxis, needed);

		// Numbers are never shortened: "2000000..." reads as another number.
		if (IsCategoryAxis(geometry))
		{
			ShortenXLabels(xAxis, labels, positions, (canvas.Width * innerPlot.GetCanvasXLocationPercent() / 100, canvas.Width), angle, target - labelTop - titleRoom);
		}

		return (angle, labels);
	}

	internal static double GrowXAxisMargin(Chart chart, Canvas canvas, double innerPlotPercent, AxisArea xAxis, double needed)
	{
		var stripHeight = canvas.Height * xAxis.GetCanvasHeightPercent() / 100;
		var chartAreaHeight = canvas.Height * chart.ChartArea.GetCanvasHeightPercent() / 100;
		var target = Target(stripHeight, needed, chartAreaHeight, canvas.Height * innerPlotPercent / 100);
		GrowBottomMargin(chart, (target - stripHeight) / chartAreaHeight * 100);
		return target;
	}

	/// <summary>Whether the X axis shows categories, which may be shortened, rather than numbers.</summary>
	internal static bool IsCategoryAxis(PlotGeometry geometry) => !geometry.IsHorizontalPlot && geometry.IsCategorical;

	internal static void GrowBottomMargin(Chart chart, double growthPercent)
	{
		if (growthPercent <= 0)
		{
			return;
		}

		chart.ChartArea.InnerPlot.YPositionPercent += growthPercent;
		chart.ChartArea.InnerPlot.HeightPercent -= growthPercent;
		chart.ChartArea.XAxis.HeightPercent += growthPercent;
	}

	/// <summary>
	/// X labels shortened to the room they have. A level label is centred on its tick, so it is
	/// limited by the nearer image edge; a turned one hangs down and to the left of its tick, so it
	/// is limited by the depth of the strip and by the space left of its tick. The plot edges are the
	/// image position of the plot's left edge, and of the image's right edge.
	/// </summary>
	internal static void ShortenXLabels(AxisArea xAxis, List<string?> labels, List<double> positions, (double PlotLeft, double ImageRight) plotEdges, int angle, double depth)
	{
		var fontSize = xAxis.FontSize;
		var sine = Sine(angle);
		var cosine = Math.Abs(Math.Cos(angle * Math.PI / 180));
		var deepest = angle == 0 ? double.MaxValue : (depth - (fontSize * (0.8 + (0.2 * sine)))) / sine;
		for (var i = 0; i < labels.Count; i++)
		{
			var tick = plotEdges.PlotLeft + positions[i];
			var widest = angle == 0
				? 2 * Math.Min(tick - EdgePaddingPixels, plotEdges.ImageRight - tick - EdgePaddingPixels)
				: Math.Min(deepest, WidthLeftOfTick(tick - EdgePaddingPixels, cosine, sine, fontSize));
			labels[i] = Fit(labels[i], widest, xAxis);
		}
	}
}
