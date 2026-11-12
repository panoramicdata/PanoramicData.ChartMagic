using System.Drawing;

namespace PanoramicData.ChartMagic.Models;

/// <summary>
/// The settings a <see cref="Series"/> and a <see cref="SeriesSpecification"/> share, held once.
/// </summary>
internal sealed class SeriesSettings
{
	internal List<ChartPoint> Points { get; set; } = [];

	internal SeriesChartType ChartType { get; set; }

	internal bool IsXValueIndexed { get; set; }

	internal string? LabelText { get; set; }

	internal string? LegendText { get; set; }

	internal MarkerStyle MarkerStyle { get; set; } = MarkerStyle.None;

	internal Color? MarkerStrokeColor { get; set; }

	internal Color? MarkerFillColor { get; set; }

	internal double? MarkerStrokeWidth { get; set; }

	internal double? MarkerSize { get; set; }

	internal double? DoughnutRadiusPercent { get; set; }

	internal PieLabelStyle PieLabelStyle { get; set; }

	internal Color? PieCollectedColor { get; set; }

	internal string? PieCollectedLabel { get; set; }
}
