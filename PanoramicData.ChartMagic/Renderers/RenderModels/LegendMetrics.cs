namespace PanoramicData.ChartMagic.Renderers.RenderModels;

/// <summary>
/// The pixel measurements a legend is laid out in.
/// </summary>
/// <param name="Width">The legend width, in pixels.</param>
/// <param name="Height">The legend height, in pixels.</param>
/// <param name="FontSize">The legend font size.</param>
/// <param name="SwatchWidth">The width of one swatch.</param>
/// <param name="SwatchHeight">The height of one swatch.</param>
/// <param name="Padding">The gap used between a swatch and its label, and between entries.</param>
internal readonly record struct LegendMetrics(
	double Width,
	double Height,
	double FontSize,
	double SwatchWidth,
	double SwatchHeight,
	double Padding)
{
	/// <summary>
	/// The measurements for a legend of the given size, at its own font size.
	/// </summary>
	/// <remarks>
	/// The swatch proportions were measured on two legends against the reference render: a
	/// 12-point legend drew swatches 32 by 14 and a 20-point one 52 by 23. Both are close to 2.6
	/// and 1.15 times the font size, and the shape matters - a square swatch, which is what this
	/// drew, is less than a third of the area and reads as a different chart.
	/// </remarks>
	internal static LegendMetrics For(double width, double height, double fontSize)
		=> new(
			Width: width,
			Height: height,
			FontSize: fontSize,
			SwatchWidth: Math.Round(fontSize * 2.6, 2),
			SwatchHeight: Math.Round(fontSize * 1.15, 2),
			Padding: Math.Round(fontSize * 0.5, 2));
}
