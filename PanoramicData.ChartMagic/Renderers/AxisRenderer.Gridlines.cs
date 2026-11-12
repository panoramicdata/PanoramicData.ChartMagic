using static PanoramicData.ChartMagic.Renderers.AxisTicks;
using static PanoramicData.ChartMagic.Renderers.AxisGridValues;

namespace PanoramicData.ChartMagic.Renderers;

internal sealed partial class AxisRenderer
{
	/// <summary>
	/// Draws gridlines across the plot for whichever axes asked for them.
	/// </summary>
	/// <remarks>
	/// Issue #31: gridlines belong to the axis whose values they mark, so the Y axis draws
	/// horizontal lines and the X axis vertical ones.
	/// </remarks>
	internal void PlotGridlines(Chart chart, PlotGeometry geometry, XmlElement innerPlotNode)
	{
		var xAxis = chart.ChartArea.XAxis;
		var yAxis = chart.ChartArea.YAxis;

		if (!yAxis.MajorGridEnabled && !yAxis.MinorGridEnabled && !xAxis.MajorGridEnabled && !xAxis.MinorGridEnabled)
		{
			return;
		}

		var gridNode = _canvas.Group("gridlines");
		innerPlotNode.AppendChild(gridNode);

		PlotHorizontalGridlines(chart, geometry, yAxis, gridNode);
		PlotVerticalGridlines(chart, geometry, xAxis, gridNode);
	}

	/// <summary>
	/// The horizontal gridlines, which mark the values on the Y axis.
	/// </summary>
	/// <remarks>
	/// Minor lines before major, so a major line is drawn over a coincident minor one.
	/// </remarks>
	private void PlotHorizontalGridlines(Chart chart, PlotGeometry geometry, AxisArea yAxis, XmlElement gridNode)
	{
		if (yAxis.MinorGridEnabled)
		{
			foreach (var value in MinorTicks(yAxis, geometry, isValueAxis: !geometry.IsHorizontalPlot))
			{
				var y = geometry.YToPixels(value);
				gridNode.AppendChild(_canvas.Line(0, y, geometry.Width, y, yAxis.MinorGridColor, yAxis.MinorGridWidth ?? yAxis.GridWidth, yAxis.MinorGridDashStyle));
			}
		}

		if (!yAxis.MajorGridEnabled)
		{
			return;
		}

		foreach (var value in HorizontalMajorGridValues(chart, geometry))
		{
			var y = geometry.IsHorizontalPlot ? geometry.CategoryToPixels(value) : geometry.YToPixels(value);
			gridNode.AppendChild(_canvas.Line(0, y, geometry.Width, y, yAxis.MajorGridColor, yAxis.GridWidth, yAxis.MajorGridDashStyle));
		}
	}

	/// <summary>
	/// The vertical gridlines, which mark the values on the X axis.
	/// </summary>
	private void PlotVerticalGridlines(Chart chart, PlotGeometry geometry, AxisArea xAxis, XmlElement gridNode)
	{
		if (xAxis.MinorGridEnabled)
		{
			foreach (var x in MinorGridPositions(xAxis, geometry))
			{
				gridNode.AppendChild(_canvas.Line(x, 0, x, geometry.Height, xAxis.MinorGridColor, xAxis.MinorGridWidth ?? xAxis.GridWidth, xAxis.MinorGridDashStyle));
			}
		}

		if (!xAxis.MajorGridEnabled)
		{
			return;
		}

		foreach (var value in VerticalMajorGridValues(chart, geometry))
		{
			var x = XAxisPixels(geometry, value);
			gridNode.AppendChild(_canvas.Line(x, 0, x, geometry.Height, xAxis.MajorGridColor, xAxis.GridWidth, xAxis.MajorGridDashStyle));
		}
	}
}
