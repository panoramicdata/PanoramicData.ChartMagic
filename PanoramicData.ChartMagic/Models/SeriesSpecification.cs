using System.Drawing;
using System.Text.Json.Serialization;

namespace PanoramicData.ChartMagic.Models;

public class SeriesSpecification
{
	private readonly SeriesSettings _shared = new();

	[JsonPropertyOrder(0)]
	public List<ChartPoint> Points
	{
		get { return _shared.Points; }
		set { _shared.Points = value; }
	}

	[JsonPropertyOrder(1)]
	public SeriesChartType ChartType
	{
		get { return _shared.ChartType; }
		set { _shared.ChartType = value; }
	}

	[JsonPropertyOrder(2)]
	public bool IsXValueIndexed
	{
		get { return _shared.IsXValueIndexed; }
		set { _shared.IsXValueIndexed = value; }
	}

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
	public string? LabelText
	{
		get { return _shared.LabelText; }
		set { _shared.LabelText = value; }
	}

	[JsonPropertyOrder(9)]
	public string? LegendText
	{
		get { return _shared.LegendText; }
		set { _shared.LegendText = value; }
	}

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

	[JsonPropertyOrder(13)]
	public MarkerStyle MarkerStyle
	{
		get { return _shared.MarkerStyle; }
		set { _shared.MarkerStyle = value; }
	}

	[JsonPropertyOrder(14)]
	public Color? MarkerStrokeColor
	{
		get { return _shared.MarkerStrokeColor; }
		set { _shared.MarkerStrokeColor = value; }
	}

	[JsonPropertyOrder(15)]
	public Color? MarkerFillColor
	{
		get { return _shared.MarkerFillColor; }
		set { _shared.MarkerFillColor = value; }
	}

	[JsonPropertyOrder(16)]
	public double? MarkerStrokeWidth
	{
		get { return _shared.MarkerStrokeWidth; }
		set { _shared.MarkerStrokeWidth = value; }
	}

	[JsonPropertyOrder(17)]
	public double? MarkerSize
	{
		get { return _shared.MarkerSize; }
		set { _shared.MarkerSize = value; }
	}

	/// <summary>
	/// Overrides <see cref="Series.DoughnutRadiusPercent"/> for this series. Null takes the
	/// chart-wide <see cref="ChartSpecification.DoughnutRadius"/> instead.
	/// </summary>
	[JsonPropertyOrder(18)]
	public double? DoughnutRadiusPercent
	{
		get { return _shared.DoughnutRadiusPercent; }
		set { _shared.DoughnutRadiusPercent = value; }
	}

	[JsonPropertyOrder(19)]
	public PieLabelStyle PieLabelStyle
	{
		get { return _shared.PieLabelStyle; }
		set { _shared.PieLabelStyle = value; }
	}

	[JsonPropertyOrder(20)]
	public Color? PieLineColor { get; set; }

	[JsonPropertyOrder(21)]
	public double? PieStartAngleDegrees { get; set; }

	[JsonPropertyOrder(22)]
	public double? PieCollectedThresholdPercent { get; set; }

	[JsonPropertyOrder(23)]
	public Color? PieCollectedColor
	{
		get { return _shared.PieCollectedColor; }
		set { _shared.PieCollectedColor = value; }
	}

	[JsonPropertyOrder(24)]
	public string? PieCollectedLabel
	{
		get { return _shared.PieCollectedLabel; }
		set { _shared.PieCollectedLabel = value; }
	}
}
