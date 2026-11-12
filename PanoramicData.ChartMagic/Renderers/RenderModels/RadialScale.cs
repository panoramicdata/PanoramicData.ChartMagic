namespace PanoramicData.ChartMagic.Renderers.RenderModels;

/// <summary>
/// Where the centre of a radial chart is, how far its rim is, and the value range the rim spans.
/// </summary>
/// <param name="CentreX">The centre, across the plot.</param>
/// <param name="CentreY">The centre, down the plot.</param>
/// <param name="Radius">The distance from the centre to the outermost ring.</param>
/// <param name="Start">The value at the centre.</param>
/// <param name="Range">The span of values from the centre to the rim.</param>
internal sealed record RadialScale(double CentreX, double CentreY, double Radius, double Start, double Range)
{
	/// <summary>
	/// How far from the centre a value is drawn, clamped to the rim.
	/// </summary>
	internal double RadiusOf(double value)
		=> Range <= 0 ? 0 : Radius * Math.Clamp((value - Start) / Range, 0, 1);

	/// <summary>
	/// A point at an angle clockwise from twelve o'clock and a distance from the centre.
	/// </summary>
	internal (double X, double Y) PointAt(double degrees, double distance)
	{
		var radians = degrees * Math.PI / 180;
		return (
			Math.Round(CentreX + (distance * Math.Sin(radians)), 2),
			Math.Round(CentreY - (distance * Math.Cos(radians)), 2));
	}
}
