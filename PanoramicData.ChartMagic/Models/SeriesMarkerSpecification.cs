using System.Drawing;
using System.Text.Json.Serialization;

namespace PanoramicData.ChartMagic.Models;

/// <summary>
/// The marker settings of a series specification.
/// </summary>
/// <remarks>
/// The orders keep these properties where they were serialised before they moved to a base class.
/// </remarks>
public abstract class SeriesMarkerSpecification
{
	[JsonPropertyOrder(13)]
	public MarkerStyle MarkerStyle { get; set; } = MarkerStyle.None;

	[JsonPropertyOrder(14)]
	public Color? MarkerStrokeColor { get; set; }

	[JsonPropertyOrder(15)]
	public Color? MarkerFillColor { get; set; }

	[JsonPropertyOrder(16)]
	public double? MarkerStrokeWidth { get; set; }

	[JsonPropertyOrder(17)]
	public double? MarkerSize { get; set; }
}
