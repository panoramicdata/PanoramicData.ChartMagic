using System.Drawing;

namespace PanoramicData.ChartMagic.Models;

public class Series(ChartElement parent, string name) : ChartNamedElement(parent, name)
{
	private readonly SeriesSettings _common = new();

	public List<ChartPoint> Points { get => _common.Points; set => _common.Points = value; }
	public SeriesChartType ChartType { get => _common.ChartType; set => _common.ChartType = value; }
	public bool IsXValueIndexed { get => _common.IsXValueIndexed; set => _common.IsXValueIndexed = value; }
	public string? LabelText { get => _common.LabelText; set => _common.LabelText = value; }
	public string? LegendText { get => _common.LegendText; set => _common.LegendText = value; }

	/// <summary>The fill drawn behind each data label, or null for none.</summary>
	public Color? LabelBackColor { get; set; }
	public MarkerStyle MarkerStyle { get => _common.MarkerStyle; set => _common.MarkerStyle = value; }
	public Color? MarkerStrokeColor { get => _common.MarkerStrokeColor; set => _common.MarkerStrokeColor = value; }
	public Color? MarkerFillColor { get => _common.MarkerFillColor; set => _common.MarkerFillColor = value; }
	public double? MarkerStrokeWidth { get => _common.MarkerStrokeWidth; set => _common.MarkerStrokeWidth = value; }
	public double? MarkerSize { get => _common.MarkerSize; set => _common.MarkerSize = value; }

	/// <summary>
	/// The width of a doughnut ring, as a percentage of its radius. Null takes the Microsoft
	/// chart control default of 60.
	/// </summary>
	/// <remarks>
	/// The ring, not the hole - the hole is what is left over, so 60 leaves a 40% hole and 30
	/// leaves a 70% one. Named for the property it mirrors on the Microsoft chart control,
	/// which carries the same slightly surprising meaning.
	/// </remarks>
	public double? DoughnutRadiusPercent
	{
		get => _common.DoughnutRadiusPercent;
		set => _common.DoughnutRadiusPercent = value;
	}

	/// <summary>Where slice labels are drawn on a pie or doughnut.</summary>
	public PieLabelStyle PieLabelStyle { get => _common.PieLabelStyle; set => _common.PieLabelStyle = value; }

	/// <summary>The colour of the line between a slice and its label, when drawn outside.</summary>
	public Color PieLineColor { get; set; } = Color.Black;

	/// <summary>
	/// Where the first slice starts, in degrees clockwise from twelve o clock.
	/// </summary>
	public double PieStartAngleDegrees { get; set; }

	/// <summary>
	/// Slices smaller than this percentage of the total are combined into a single slice.
	/// Zero disables it.
	/// </summary>
	public double PieCollectedThresholdPercent { get; set; }

	/// <summary>The colour of the combined slice.</summary>
	public Color? PieCollectedColor { get => _common.PieCollectedColor; set => _common.PieCollectedColor = value; }

	/// <summary>The label and legend entry for the combined slice.</summary>
	public string? PieCollectedLabel { get => _common.PieCollectedLabel; set => _common.PieCollectedLabel = value; }
}
