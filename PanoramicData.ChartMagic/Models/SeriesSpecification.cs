using System.Drawing;
using System.Text.Json.Serialization;

namespace PanoramicData.ChartMagic.Models;

public class SeriesSpecification : SeriesMarkerSpecification
{
	[JsonPropertyOrder(0)]
	public List<ChartPoint> Points { get; set; } = [];

	[JsonPropertyOrder(1)]
	public SeriesChartType ChartType { get; set; }

	[JsonPropertyOrder(2)]
	public bool IsXValueIndexed { get; set; }

	[JsonPropertyOrder(3)]
	public Color FillColor { get; set; } = Colors.Transparent;

	[JsonPropertyOrder(4)]
	public Color StrokeColor { get; set; }

	[JsonPropertyOrder(5)]
	public double StrokeWidth { get; set; } = 2;

	[JsonPropertyOrder(6)]
	public StrokeLineCapStyle StrokeLineCapStyle { get; set; } = StrokeLineCapStyle.Round;

	[JsonPropertyOrder(7)]
	public StrokeLineJoinStyle StrokeLineJoinStyle { get; set; } = StrokeLineJoinStyle.Round;

	[JsonPropertyOrder(8)]
	public string? LabelText { get; set; }

	[JsonPropertyOrder(9)]
	public string? LegendText { get; set; }

	[JsonPropertyOrder(10)]
	public double FontSize { get; set; } = 20;

	/// <summary>
	/// The colour of this series' labels: data labels, pie labels and funnel labels. Black, as the
	/// Microsoft chart control's default.
	/// </summary>
	[JsonPropertyOrder(11)]
	public Color FontColor { get; set; } = Color.Black;

	[JsonPropertyOrder(12)]
	public ChartDashStyle StrokeStyle { get; set; } = ChartDashStyle.Solid;

	/// <summary>
	/// Overrides <see cref="Series.DoughnutRadiusPercent"/> for this series. Null takes the
	/// chart-wide <see cref="ChartSpecification.DoughnutRadius"/> instead.
	/// </summary>
	[JsonPropertyOrder(18)]
	public double? DoughnutRadiusPercent { get; set; }

	[JsonPropertyOrder(19)]
	public PieLabelStyle PieLabelStyle { get; set; }

	[JsonPropertyOrder(20)]
	public Color? PieLineColor { get; set; }

	[JsonPropertyOrder(21)]
	public double? PieStartAngleDegrees { get; set; }

	[JsonPropertyOrder(22)]
	public double? PieCollectedThresholdPercent { get; set; }

	[JsonPropertyOrder(23)]
	public Color? PieCollectedColor { get; set; }

	[JsonPropertyOrder(24)]
	public string? PieCollectedLabel { get; set; }
}
