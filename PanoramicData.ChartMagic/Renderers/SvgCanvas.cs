using System.Drawing;

namespace PanoramicData.ChartMagic.Renderers;

/// <summary>
/// The SVG document being written, and the primitives every part of a chart is drawn with.
/// </summary>
/// <remarks>
/// One canvas is shared by the renderers for the axes, the legend, the series, the pie and the
/// markers. They each draw a different part of the chart but all write into the same document and
/// share the same output size, so that is what this holds: they take it as a dependency rather
/// than being parts of one large class.
/// </remarks>
/// <param name="widthPixels">The output width.</param>
/// <param name="heightPixels">The output height.</param>
/// <param name="debug">Whether to label each positioned group with the element it represents.</param>
internal sealed class SvgCanvas(int widthPixels, int heightPixels, bool debug)
{
	/// <summary>
	/// The font named on every text node when the chart does not name one.
	/// </summary>
	/// <remarks>
	/// Issue #60. Led by the embedded face so an SVG opened elsewhere matches our own raster
	/// output, then Arial and the generics for consumers that have neither.
	/// </remarks>
	private const string DefaultFontFamilyStack = "Liberation Sans, Arial, Helvetica, sans-serif";

	/// <summary>The document being built.</summary>
	internal XmlDocument Document { get; } = new();

	/// <summary>The output width, in pixels.</summary>
	internal int WidthPixels => widthPixels;

	/// <summary>The output height, in pixels.</summary>
	internal int HeightPixels => heightPixels;

	/// <summary>
	/// An element in no namespace, which is how every node in this document is created.
	/// </summary>
	internal XmlElement Element(string name) => Document.CreateElement(string.Empty, name, string.Empty);

	/// <summary>
	/// A group node carrying nothing but an id.
	/// </summary>
	internal XmlElement Group(string id)
	{
		var groupNode = Element("g");
		groupNode.SetAttribute("id", id);
		return groupNode;
	}

	/// <summary>
	/// Creates a text node at an absolute position within its enclosing group.
	/// </summary>
	/// <remarks>
	/// Issue #35: the stroke colour is now skipped when transparent. It used to be written
	/// unconditionally, and because <c>ToHex</c> discards alpha a transparent colour became
	/// <c>#000000</c> - a black outline around every label whether or not one was asked for.
	/// The font size is emitted too; it was carried on every element and never used, so all
	/// text rendered at the SVG default size regardless of what was set.
	/// </remarks>
	internal XmlElement Text(
		string id,
		double x,
		double y,
		string text,
		HorizontalAlignment horizontalAlignment,
		VerticalAlignment verticalAlignment,
		TextStyle style,
		double rotationDegrees = 0)
	{
		var roundedX = Math.Round(x, 2);
		var roundedY = Math.Round(y + BaselineOffset(verticalAlignment, style.FontSize), 2);

		var textNode = Element("text");
		textNode.SetAttribute("x", roundedX.ToString(CultureInfo.InvariantCulture));
		textNode.SetAttribute("y", roundedY.ToString(CultureInfo.InvariantCulture));
		textNode.SetAttribute("id", id);
		textNode.InnerText = text;
		textNode.SetAttribute("text-anchor", TextAnchor(horizontalAlignment));

		textNode.SetAttribute("font-weight", style.FontWeight.ToString().ToLowerInvariant());
		textNode.SetAttribute("font-size", style.FontSize.ToString(CultureInfo.InvariantCulture));

		// Issue #60: always name a font, defaulting to the one embedded in this assembly. That
		// does not affect our own raster output, where EmbeddedTypefaceProvider answers every
		// request regardless, but SVG is a public output format and a browser or an editor
		// opening one would otherwise pick its own default and lay the text out differently
		// from the PNG of the same chart. The stack is for those consumers, since Svg.Skia
		// ignores everything after the first entry.
		textNode.SetAttribute(
			"font-family",
			style.FontFamily is { Length: > 0 } ? style.FontFamily : DefaultFontFamilyStack);

		if (style.StrokeColor != Colors.Transparent)
		{
			textNode.SetAttribute("stroke", style.StrokeColor.ToHex());
		}

		textNode.SetAttribute("fill", style.FillColor.ToHex());

		if (Math.Abs(rotationDegrees) > 1e-10)
		{
			textNode.SetAttribute(
				"transform",
				FormattableString.Invariant($"rotate({rotationDegrees} {roundedX} {roundedY})"));
		}

		return textNode;
	}

	/// <summary>
	/// How far below the requested Y the baseline sits, for a vertical alignment.
	/// </summary>
	/// <remarks>
	/// Vertical alignment is resolved here rather than left to the renderer.
	///
	/// alignment-baseline is inconsistently supported: browsers largely ignore it on a bare
	/// text element, and the raster path through Svg.Skia ignores it outright, so every label
	/// fell back to the alphabetic baseline and sat higher than intended. Measured against
	/// DocMagic, X axis labels landed at y 341-348 where the reference put them at 348-359.
	///
	/// Offsetting y by a fraction of the font size instead gives the same result in every
	/// renderer, which is the point: the browser and the PNG have to agree. The fractions are
	/// the usual approximations - an ascent of about four fifths of the em, and a visual
	/// centre about a third of the em above the baseline.
	/// </remarks>
	private static double BaselineOffset(VerticalAlignment verticalAlignment, double fontSize)
		=> verticalAlignment switch
		{
			VerticalAlignment.Top => fontSize * 0.8,
			VerticalAlignment.Middle => fontSize * 0.32,
			VerticalAlignment.Bottom => 0,
			_ => throw new NotSupportedException($"Unsupported VerticalAlignment {verticalAlignment}.")
		};

	/// <summary>
	/// The SVG text-anchor for a horizontal alignment.
	/// </summary>
	private static string TextAnchor(HorizontalAlignment horizontalAlignment)
		=> horizontalAlignment switch
		{
			HorizontalAlignment.Left => "start",
			HorizontalAlignment.Center => "middle",
			HorizontalAlignment.Right => "end",
			_ => throw new NotSupportedException($"Unsupported HorizontalAlignment {horizontalAlignment}.")
		};

	/// <summary>
	/// A filled rectangle behind a piece of text drawn by <see cref="Text"/> with the same arguments.
	/// </summary>
	/// <remarks>
	/// Sized from the measured text with a little padding, and placed from the same alignment the
	/// text is, so the two stay together whichever way the label is anchored.
	/// </remarks>
	internal XmlElement TextBackground(
		double x,
		double y,
		string text,
		HorizontalAlignment horizontalAlignment,
		VerticalAlignment verticalAlignment,
		TextStyle style,
		Color color)
	{
		const double padding = 2;
		var width = TextMeasure.Width(text, style.FontSize, style.FontWeight) + (2 * padding);
		var height = style.FontSize * 1.2;

		var left = horizontalAlignment switch
		{
			HorizontalAlignment.Left => x - padding,
			HorizontalAlignment.Right => x - width + padding,
			_ => x - (width / 2)
		};

		// From where the glyphs' visual centre ends up for each alignment; see BaselineOffset.
		var centre = verticalAlignment switch
		{
			VerticalAlignment.Top => y + (style.FontSize * 0.5),
			VerticalAlignment.Bottom => y - (style.FontSize * 0.3),
			_ => y
		};

		var rect = Element("rect");
		rect.SetAttribute("x", N(left));
		rect.SetAttribute("y", N(centre - (height / 2)));
		rect.SetAttribute("width", N(width));
		rect.SetAttribute("height", N(height));
		rect.SetAttribute("fill", color.ToHex());
		if (color.A != 255)
		{
			rect.SetAttribute("fill-opacity", (color.A / 255f).ToString("F2", CultureInfo.InvariantCulture));
		}

		return rect;
	}

	internal double RelativePositionY(ChartNamedElement chartNamedElement, double yPositionPercent)
		=> heightPixels * (100 - (yPositionPercent * chartNamedElement.GetCanvasHeightPercent() / 100)) / 100;

	internal double RelativePositionX(ChartNamedElement chartNamedElement, double xPositionPercent)
		=> widthPixels * xPositionPercent * chartNamedElement.GetCanvasWidthPercent() / 100 / 100;

	internal XmlElement Line(
		double x1,
		double y1,
		double x2,
		double y2,
		Color color,
		double width,
		ChartDashStyle dashStyle = ChartDashStyle.NotSet)
	{
		var lineNode = Element("line");
		lineNode.SetAttribute("x1", Math.Round(x1, 2).ToString(CultureInfo.InvariantCulture));
		lineNode.SetAttribute("y1", Math.Round(y1, 2).ToString(CultureInfo.InvariantCulture));
		lineNode.SetAttribute("x2", Math.Round(x2, 2).ToString(CultureInfo.InvariantCulture));
		lineNode.SetAttribute("y2", Math.Round(y2, 2).ToString(CultureInfo.InvariantCulture));
		lineNode.SetAttribute("stroke", color.ToHex());
		if (color.A != 255)
		{
			lineNode.SetAttribute(
				"stroke-opacity",
				(color.A / 255f).ToString("F2", CultureInfo.InvariantCulture));
		}

		lineNode.SetAttribute("stroke-width", width.ToString(CultureInfo.InvariantCulture));

		var dashArray = DashArrayFor(dashStyle, width);
		if (dashArray is not null)
		{
			lineNode.SetAttribute("stroke-dasharray", dashArray);
		}

		return lineNode;
	}

	/// <summary>
	/// The dash pattern for a style at a line width, or null for a solid line.
	/// </summary>
	/// <remarks>
	/// The GDI+ patterns the Microsoft chart control draws with, which are in units of the pen
	/// width: a 4 pixel dashed border has 12 pixel dashes. Fixed pixel patterns made a thick dashed
	/// line read as solid.
	/// </remarks>
	internal static string? DashArrayFor(ChartDashStyle dashStyle, double width)
	{
		double[]? pattern = dashStyle switch
		{
			ChartDashStyle.Dash => [3, 1],
			ChartDashStyle.DashDot => [3, 1, 1, 1],
			ChartDashStyle.DashDotDot => [3, 1, 1, 1, 1, 1],
			ChartDashStyle.Dot => [1, 1],
			_ => null
		};

		var unit = Math.Max(width, 1);
		return pattern is null ? null : string.Join(",", pattern.Select(length => N(length * unit)));
	}

	/// <summary>
	/// A coordinate formatted for a path, to two decimal places and culture-independently.
	/// </summary>
	/// <remarks>
	/// A path built by concatenating interpolated strings is a plain string, so it cannot be
	/// handed to FormattableString.Invariant; formatting each number as it goes is what keeps a
	/// comma-decimal culture from producing an unparseable path.
	/// </remarks>
	internal static string N(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

	/// <summary>
	/// An element's border alone, inset by half its width so it lies inside the image.
	/// </summary>
	/// <remarks>
	/// For the chart's outer border, which is drawn after everything else, as the Microsoft chart
	/// control draws its Borderline: drawn with the background, the legend's box covered it.
	/// </remarks>
	internal XmlElement? Border(ChartNamedElement element)
	{
		if (element.StrokeColor == Colors.Transparent || element.StrokeWidth <= 0)
		{
			return null;
		}

		var inset = element.StrokeWidth / 2;
		var rect = Element("rect");
		rect.SetAttribute("id", "chartBorder");
		rect.SetAttribute("x", N(inset));
		rect.SetAttribute("y", N(inset));
		rect.SetAttribute("width", N(Math.Max(0, widthPixels - element.StrokeWidth)));
		rect.SetAttribute("height", N(Math.Max(0, heightPixels - element.StrokeWidth)));
		rect.SetStyle(element, applyFill: false);
		return rect;
	}

	/// <summary>
	/// A positioned group for an element, translated into place.
	/// </summary>	/// <param name="element">The element the group represents.</param>
	/// <param name="id">The group id.</param>
	/// <param name="within">
	/// The group this one is nested inside, when it is nested inside a positioned one.
	/// </param>
	/// <remarks>
	/// Nesting matters because SVG transforms compound: a group inside a translated group is
	/// already moved by its parent, so translating it by its absolute position moves it twice.
	/// That went unnoticed because every parent sat at the origin in the common case - a chart
	/// area at 0,0 translates to "0,0", which is skipped entirely. Put the legend on the left,
	/// so the chart area starts 20% in, and the plot and its axes were displaced by a further
	/// 20% of the width: the last category fell off the canvas.
	/// </remarks>
	/// <param name="drawStroke">Whether the element's border is drawn on its rectangle.</param>
	internal XmlElement PositionedGroup(ChartNamedElement element, string id, ChartElement? within = null, bool drawStroke = true)
	{
		var groupNode = Group(id);

		// Y is measured from the bottom here and from the top in SVG, so a position becomes a
		// distance from the top of the element above it.
		var topPercent = 100 - (element.GetCanvasYLocationPercent() + element.GetCanvasHeightPercent());
		var leftPercent = element.GetCanvasXLocationPercent();

		if (within is not null)
		{
			leftPercent -= within.GetCanvasXLocationPercent();
			topPercent -= 100 - (within.GetCanvasYLocationPercent() + within.GetCanvasHeightPercent());
		}

		var translation = $"{widthPixels * leftPercent / 100},{heightPixels * topPercent / 100}";
		if (translation != "0,0")
		{
			groupNode.SetAttribute("transform", $"translate({translation})");
		}

		var rectNode = Element("rect");
		var width = widthPixels * element.GetCanvasWidthPercent() / 100;
		var height = heightPixels * element.GetCanvasHeightPercent() / 100;

		// A stroke is centred on the edge, so a border on an element filling the image lost half
		// its width off the edge. Inset by half the stroke, the border lies inside the box, as the
		// Microsoft chart control draws it.
		if (drawStroke && element.StrokeColor != Colors.Transparent && element.StrokeWidth > 0)
		{
			var inset = element.StrokeWidth / 2;
			rectNode.SetAttribute("x", inset.ToString(CultureInfo.InvariantCulture));
			rectNode.SetAttribute("y", inset.ToString(CultureInfo.InvariantCulture));
			width = Math.Max(0, width - element.StrokeWidth);
			height = Math.Max(0, height - element.StrokeWidth);
		}

		rectNode.SetAttribute("width", width.ToString(CultureInfo.InvariantCulture));
		rectNode.SetAttribute("height", height.ToString(CultureInfo.InvariantCulture));
		if (element.XRadiusPixels != 0)
		{
			rectNode.SetAttribute("rx", element.XRadiusPixels.ToString(CultureInfo.InvariantCulture));
		}

		if (element.YRadiusPixels != 0)
		{
			rectNode.SetAttribute("ry", element.YRadiusPixels.ToString(CultureInfo.InvariantCulture));
		}

		rectNode.SetStyle(element, applyStroke: drawStroke);
		groupNode.AppendChild(rectNode);

		if (debug)
		{
			var debugTextNode = Element("text");
			debugTextNode.SetAttribute("alignment-baseline", "hanging");
			debugTextNode.InnerText = element.Name;
			groupNode.AppendChild(debugTextNode);
		}

		return groupNode;
	}
}
