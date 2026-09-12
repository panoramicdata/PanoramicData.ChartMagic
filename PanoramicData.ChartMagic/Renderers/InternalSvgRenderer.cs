namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// Writes a chart out as SVG.
/// </summary>
/// <remarks>
/// This builds the document skeleton and then hands each part of the chart to the renderer that
/// draws it - the axes, the legend, the series, the pie and the markers each have their own.
/// They share an <see cref="SvgCanvas"/>, which owns the document and the drawing primitives.
/// </remarks>
internal sealed class InternalSvgRenderer(int widthPixels, int heightPixels, bool debug)
{
	private readonly SvgCanvas _canvas = new(widthPixels, heightPixels, debug);

	internal void SaveImage(Stream stream, Chart chart)
	{
		Initialize(
			chart,
			out var defs,
			out var chartBackgroundAreaNode,
			out var chartAreaNode,
			out var innerPlotNode,
			out var axisHandlerResult);

		var geometry = new PlotGeometry(
			chart,
			axisHandlerResult,
			widthPixels * chart.ChartArea.InnerPlot.GetCanvasWidthPercent() / 100,
			heightPixels * chart.ChartArea.InnerPlot.GetCanvasHeightPercent() / 100);

		var legends = new LegendRenderer(_canvas);

		// A pie has no axes, so it takes a different path entirely: no gridlines, no axis
		// strips, and a legend that describes slices rather than series.
		var pieSeries = chart.Series.FirstOrDefault(PieRenderer.IsPie);
		if (pieSeries is not null)
		{
			var slices = PieSliceBuilder.Build(pieSeries);
			new PieRenderer(_canvas).Plot(pieSeries, slices, innerPlotNode, geometry.Width, geometry.Height);
			legends.PlotPieLegend(chart, slices, chartBackgroundAreaNode);
		}
		else
		{
			var axes = new AxisRenderer(_canvas);

			// Gridlines first, so that the series are drawn over them rather than under.
			axes.PlotGridlines(chart, geometry, innerPlotNode);

			new SeriesRenderer(_canvas).PlotSeries(chart, geometry, defs, innerPlotNode);

			axes.PlotAxes(chart, geometry, chartAreaNode);

			legends.PlotLegends(chart, chartBackgroundAreaNode);
		}

		PlotAnnotations(chart, chartBackgroundAreaNode);

		// Issue #27: UTF-8, not UTF-16.
		//
		// Encoding.Unicode is UTF-16 LE. A UTF-16 SVG is valid and renders fine in a browser,
		// but Chart.SaveImage produces raster output by rendering to SVG and reloading it
		// through SKSvg, and that parse silently yields an empty picture for UTF-16 input. The
		// result was a valid PNG or JPEG containing no chart content at all - the background
		// fill and nothing else - while the SVG of the same chart was complete.
		//
		// UTF-8 is also what consumers expect of an .svg file; reading one with a UTF-8 reader
		// previously failed on the first byte.
		var writer = new XmlTextWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
		{
			Formatting = Formatting.Indented
		};
		_canvas.Document.WriteContentTo(writer);
		writer.Flush();
	}

	private void Initialize(
		Chart chart,
		out XmlElement defs,
		out XmlElement chartBackgroundAreaNode,
		out XmlElement chartAreaNode,
		out XmlElement innerPlotNode,
		out AxisHandlerResult axisHandlerResult)
	{
		var document = _canvas.Document;

		// Issue #27: must match the encoding the writer actually uses, or the declaration
		// contradicts the bytes and parsing fails outright.
		var xmlDeclaration = document.CreateXmlDeclaration("1.0", "UTF-8", "yes");
		var root = document.DocumentElement;
		document.InsertBefore(xmlDeclaration, root);

		var svg = _canvas.Element("svg");
		svg.SetAttribute("xmlns", "http://www.w3.org/2000/svg");
		svg.SetAttribute("xmlns:xlink", "http://www.w3.org/1999/xlink");
		document.AppendChild(svg);
		svg.SetAttribute("width", widthPixels.ToString(CultureInfo.InvariantCulture));
		svg.SetAttribute("height", heightPixels.ToString(CultureInfo.InvariantCulture));

		// Issue #27: a viewBox is required, not optional.
		//
		// Chart.SaveImage produces raster output by rendering to SVG and reloading it through
		// SKSvg, then drawing the resulting picture scaled to the requested pixel size. With
		// width and height but no viewBox there is no user coordinate system to scale from, so
		// the picture bounds fall back to the content bounds and the scale-to-fit blows the
		// drawing up. The visible result was a raster image consisting of one enormously
		// magnified element - a background rectangle - with everything else pushed off canvas.
		//
		// Browsers tolerate the omission because they treat width and height as the viewport,
		// which is why the SVG output looked correct while the PNG did not.
		svg.SetAttribute(
			"viewBox",
			FormattableString.Invariant($"0 0 {widthPixels} {heightPixels}"));

		// Always define a defs node
		defs = _canvas.Element("defs");
		svg.AppendChild(defs);

		// Chart background area
		chartBackgroundAreaNode = _canvas.PositionedGroup(chart.ChartBackgroundArea, "chartBackgroundArea");
		svg.AppendChild(chartBackgroundAreaNode);

		// ChartArea background
		chartAreaNode = _canvas.PositionedGroup(chart.ChartArea, "chartArea", chart.ChartBackgroundArea);
		chartBackgroundAreaNode.AppendChild(chartAreaNode);

		// Inner Plot background
		innerPlotNode = _canvas.PositionedGroup(chart.ChartArea.InnerPlot, "innerPlot", chart.ChartArea);
		chartAreaNode.AppendChild(innerPlotNode);

		axisHandlerResult = new AxisHandler(chart).Process();
	}

	private void PlotAnnotations(Chart chart, XmlElement chartBackgroundAreaNode)
	{
		// Annotations
		var annotationIndex = 0;
		foreach (var annotation in chart.Annotations)
		{
			var textNode = _canvas.Text(
				$"annotation{annotationIndex++}",
				_canvas.RelativePositionX(chart.ChartBackgroundArea, annotation.GetCanvasXLocationPercent()),
				_canvas.RelativePositionY(chart.ChartBackgroundArea, annotation.GetCanvasYLocationPercent()),
				annotation.Text,
				annotation.HorizontalAlignment,
				annotation.VerticalAlignment,
				new TextStyle(
					annotation.FontWeight,
					annotation.FontFamily,
					annotation.FontSize,
					annotation.StrokeColor,
					annotation.FillColor));
			chartBackgroundAreaNode.AppendChild(textNode);
		}
	}
}
