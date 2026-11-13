using System.Drawing;
using System.Text.Json.Serialization;

namespace PanoramicData.ChartMagic.Models;

/// <summary>
/// One data point.
/// </summary>
/// <param name="XValueString">The label for the point, where the axis is categorical.</param>
/// <param name="XValue">The position of the point along the X axis.</param>
/// <param name="YValue">The value of the point, or null where there is no value.</param>
/// <param name="Color">
/// The colour of this point specifically, overriding the colour of its series. Pie and doughnut
/// charts colour every slice separately, so this is how they are coloured; for other chart types
/// it is normally left null and the series colour used.
/// </param>
/// <param name="LegendText">
/// The legend entry for this point. Only pie and doughnut charts give each point its own legend
/// entry; elsewhere the legend describes the series.
/// </param>
public record ChartPoint(
	string? XValueString,
	double XValue,
	double? YValue,
	Color? Color = null,
	string? LegendText = null)
{
	/// <summary>
	/// The several Y values a point carries where one is not enough. A box plot takes six or more, in
	/// the Microsoft chart control's order: lower whisker, upper whisker, lower box, upper box,
	/// average, median, then any further values drawn as unusual points. Null for every other chart
	/// type, and left out of the serialised form when null.
	/// </summary>
	[JsonPropertyOrder(5)]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<double>? YValues { get; init; }
}
