using PanoramicData.ChartMagic.Extensions;
using PanoramicData.ChartMagic.Models;
using System.Drawing;

namespace PanoramicData.ChartMagic.Demo.Services;

/// <summary>
/// The specifications behind the gallery: one builder per sample, and the small helpers that
/// decorate a specification with axes, a range, a legend position and so on.
/// </summary>
/// <remarks>
/// Separate from <see cref="SampleCharts"/>, which owns the gallery order and the rendering,
/// because this is the part that changes whenever a sample is added.
/// </remarks>
internal static class SampleChartBuilders
{
	private static readonly string[] Days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

	internal static List<ChartPoint> Points(params double[] values)
	{
		var points = new List<ChartPoint>();
		for (var i = 0; i < values.Length; i++)
		{
			// The label makes the axis categorical; the index positions it.
			points.Add(new ChartPoint(Days[i % Days.Length], i, values[i]));
		}

		return points;
	}

	internal static ChartSpecification WithAxes(ChartSpecification specification, string xTitle, string yTitle)
	{
		specification.XAxisTitle = xTitle;
		specification.YAxisTitle = yTitle;
		specification.YAxisMajorGridEnabled = true;
		return specification;
	}

	internal static ChartSpecification Build(SeriesChartType chartType, bool markers = false)
		=> new()
		{
			SeriesList =
			[
				new()
				{
					ChartType = chartType,
					LegendText = "Utilisation",
					StrokeColor = Color.SteelBlue,
					FillColor = chartType == SeriesChartType.Line ? Colors.Transparent : Color.LightSteelBlue,
					StrokeWidth = 3,
					IsXValueIndexed = true,
					MarkerStyle = markers ? MarkerStyle.Circle : MarkerStyle.None,
					MarkerFillColor = markers ? Color.White : null,
					MarkerStrokeColor = markers ? Color.SteelBlue : null,
					MarkerSize = markers ? 4 : null,
					Points = Points(12, 19, 14, 22, 26, 21, 30)
				}
			]
		};

	internal static ChartSpecification BuildMultiSeries(SeriesChartType chartType)
	{
		var colours = new[] { Color.SteelBlue, Color.SeaGreen, Color.Goldenrod };
		var names = new[] { "CPU", "Memory", "Disk" };
		var data = new[]
		{
			new double[] { 12, 19, 14, 22, 26, 21, 30 },
			new double[] { 8, 11, 9, 14, 16, 15, 18 },
			new double[] { 4, 6, 5, 7, 9, 8, 11 }
		};

		var specification = new ChartSpecification();
		for (var i = 0; i < names.Length; i++)
		{
			specification.SeriesList.Add(new SeriesSpecification
			{
				ChartType = chartType,
				LegendText = names[i],
				StrokeColor = colours[i],
				FillColor = colours[i],
				StrokeWidth = 1,
				IsXValueIndexed = true,
				Points = Points(data[i])
			});
		}

		// Stacked series read better as a solid block than as three outlined ones.
		if (chartType == SeriesChartType.StackedArea)
		{
			specification.LegendStyle = LegendStyle.Column;
		}

		return specification;
	}

	internal static ChartSpecification WithPieLabels(ChartSpecification specification, string style)
	{
		specification.PieLabelStyle = style;
		return specification;
	}

	internal static ChartSpecification BuildMixed()
	{
		var specification = new ChartSpecification
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Column,
					LegendText = "Volume",
					StrokeColor = Color.SteelBlue,
					FillColor = Color.SteelBlue,
					StrokeWidth = 1,
					IsXValueIndexed = true,
					Points = Points(12, 19, 14, 22, 26, 21, 30)
				},
				new()
				{
					ChartType = SeriesChartType.Line,
					LegendText = "Target",
					StrokeColor = Color.IndianRed,
					StrokeWidth = 3,
					IsXValueIndexed = true,
					MarkerStyle = MarkerStyle.Circle,
					MarkerFillColor = Color.White,
					MarkerStrokeColor = Color.IndianRed,
					MarkerSize = 6,
					Points = Points(18, 18, 18, 20, 20, 22, 22)
				}
			]
		};

		return WithAxes(specification, "Day", "Percent");
	}

	internal static ChartSpecification WithRange(ChartSpecification specification, double minimum, double maximum)
	{
		specification.YAxisMinimum = minimum;
		specification.YAxisMaximum = maximum;
		return specification;
	}

	internal static ChartSpecification WithLabelAngle(ChartSpecification specification, int degrees)
	{
		specification.XAxisLabelAngle = degrees;
		return specification;
	}

	internal static ChartSpecification WithDoughnutHole(ChartSpecification specification, int percent)
	{
		specification.DoughnutRadius = percent;
		return specification;
	}

	/// <summary>
	/// The legend as a column on the right. Positions are percentages of the image measured from
	/// the bottom left, so a full-height legend on the right is x 78, y 0.
	/// </summary>
	internal static ChartSpecification WithLegendColumn(ChartSpecification specification)
	{
		specification.LegendStyle = LegendStyle.Column;
		specification.LegendXPositionPercent = 78;
		specification.LegendYPositionPercent = 0;
		specification.LegendWidthPercent = 22;
		specification.LegendHeightPercent = 100;
		specification.ChartAreaWidthPercent = 78;
		return specification;
	}

	internal static ChartSpecification WithLegendBelow(ChartSpecification specification)
	{
		specification.LegendStyle = LegendStyle.Row;
		specification.LegendXPositionPercent = 0;
		specification.LegendYPositionPercent = 0;
		specification.LegendWidthPercent = 100;
		specification.LegendHeightPercent = 16;
		specification.ChartAreaXPositionPercent = 0;
		specification.ChartAreaYPositionPercent = 16;
		specification.ChartAreaWidthPercent = 100;
		specification.ChartAreaHeightPercent = 84;
		return specification;
	}

	internal static ChartSpecification BuildMarkerGallery()
	{
		var styles = new[]
		{
			MarkerStyle.Circle,
			MarkerStyle.Square,
			MarkerStyle.Diamond,
			MarkerStyle.Triangle,
			MarkerStyle.Cross,
			MarkerStyle.Star4,
			MarkerStyle.Star5,
			MarkerStyle.Star6
		};
		var colours = new[]
		{
			Color.SteelBlue,
			Color.SeaGreen,
			Color.Goldenrod,
			Color.IndianRed,
			Color.MediumPurple,
			Color.DarkCyan,
			Color.Chocolate,
			Color.SlateGray
		};

		var specification = WithLegendColumn(new ChartSpecification { LegendStyle = LegendStyle.Column });

		for (var index = 0; index < styles.Length; index++)
		{
			// One flat series per style, stacked up the plot, so each marker is on its own line
			// and can be told apart.
			var level = 4 + (index * 4);
			specification.SeriesList.Add(new SeriesSpecification
			{
				ChartType = SeriesChartType.Line,
				LegendText = styles[index].ToString(),
				StrokeColor = colours[index],
				StrokeWidth = 2,
				IsXValueIndexed = true,
				MarkerStyle = styles[index],
				MarkerSize = 12,
				MarkerFillColor = Color.White,
				MarkerStrokeColor = colours[index],
				MarkerStrokeWidth = 2,
				Points = Points(level, level, level, level, level)
			});
		}

		return WithAxes(specification, "Point", "Series");
	}

	internal static ChartSpecification BuildGridlines()
	{
		var specification = WithAxes(BuildMultiSeries(SeriesChartType.Line), "Day", "Percent");
		specification.XAxisMajorGridEnabled = true;
		specification.YAxisMajorGridEnabled = true;
		specification.YAxisMinorGridEnabled = true;
		specification.XAxisMajorGridColor = Color.FromArgb(0xC0, 0xC8, 0xD0);
		specification.YAxisMajorGridColor = Color.FromArgb(0xC0, 0xC8, 0xD0);
		specification.YAxisMinorGridColor = Color.FromArgb(0xE8, 0xEC, 0xF0);
		specification.LegendStyle = LegendStyle.Column;
		return WithLegendColumn(specification);
	}

	internal static ChartSpecification BuildFormatted()
	{
		var specification = new ChartSpecification
		{
			YAxisLabelFormat = "#,##0.0",
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Column,
					LegendText = "Requests",
					StrokeColor = Color.SteelBlue,
					FillColor = Color.SteelBlue,
					StrokeWidth = 1,
					IsXValueIndexed = true,
					Points = Points(1240, 1890, 1410, 2260, 2610, 2130, 3020)
				}
			]
		};

		return WithAxes(specification, "Day", "Requests");
	}

	internal static ChartSpecification BuildLongCategories()
	{
		var regions = new[] { "North West", "North East", "Midlands", "South West", "South East" };
		var values = new double[] { 42, 31, 55, 28, 61 };

		return new ChartSpecification
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Column,
					LegendText = "Sales",
					StrokeColor = Color.SeaGreen,
					FillColor = Color.SeaGreen,
					StrokeWidth = 1,
					IsXValueIndexed = true,
					Points = [.. values.Select((value, i) => new ChartPoint(regions[i], i, value))]
				}
			]
		};
	}

	internal static ChartSpecification BuildNegative()
	{
		var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
		var values = new double[] { 18, -7, 12, -14, 4, 21 };

		return new ChartSpecification
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Column,
					LegendText = "Net change",
					StrokeColor = Color.SteelBlue,
					FillColor = Color.SteelBlue,
					StrokeWidth = 1,
					IsXValueIndexed = true,
					Points = [.. values.Select((value, i) => new ChartPoint(months[i], i, value))]
				}
			]
		};
	}

	internal static ChartSpecification BuildSinglePoint()
		=> new()
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Column,
					LegendText = "Utilisation",
					StrokeColor = Color.Goldenrod,
					FillColor = Color.Goldenrod,
					StrokeWidth = 1,
					IsXValueIndexed = true,
					Points = [new ChartPoint("Mon", 0, 42)]
				}
			]
		};

	internal static ChartSpecification BuildManyCategories()
	{
		var points = new List<ChartPoint>();
		for (var hour = 0; hour < 24; hour++)
		{
			// A plausible daily shape: quiet overnight, busy through the working day.
			var value = 40 + (60 * Math.Sin(Math.Max(0, hour - 5) / 14.0 * Math.PI));
			points.Add(new ChartPoint(
				FormattableString.Invariant($"{hour:00}:00"),
				hour,
				Math.Round(Math.Max(8, value))));
		}

		return new ChartSpecification
		{
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Line,
					LegendText = "Requests",
					StrokeColor = Color.IndianRed,
					StrokeWidth = 2,
					IsXValueIndexed = true,
					Points = points
				}
			]
		};
	}

	internal static ChartSpecification BuildPie(SeriesChartType chartType)
	{
		var colours = new[]
		{
			Color.SteelBlue,
			Color.SeaGreen,
			Color.Goldenrod,
			Color.IndianRed,
			Color.MediumPurple
		};
		var names = new[] { "London", "Manchester", "Leeds", "Bristol", "Glasgow" };
		var values = new double[] { 34, 26, 18, 13, 9 };

		return new ChartSpecification
		{
			SeriesList =
			[
				new()
				{
					ChartType = chartType,
					StrokeColor = Color.White,
					StrokeWidth = 1,
					Points =
					[
						.. values.Select((value, index) => new ChartPoint(
							names[index],
							index,
							value,
							colours[index]))
					]
				}
			]
		};
	}

	internal static ChartSpecification BuildCollectedPie()
	{
		var specification = BuildPie(SeriesChartType.Pie);
		specification.PieLabelStyle = "Outside";
		specification.PieCollectedThresholdPercent = 15;
		specification.PieCollectedLabel = "Other";
		specification.PieCollectedColor = Color.DarkGray;
		specification.SeriesList[0].LabelText = "#PERCENT";
		return specification;
	}

	internal static ChartSpecification BuildWithAxisFurniture()
	{
		var specification = WithAxes(Build(SeriesChartType.Line, markers: true), "Day", "Percent");
		specification.XAxisMajorGridEnabled = true;
		specification.YAxisMinorGridEnabled = true;
		specification.XAxisLabelAngle = -45;
		specification.LegendStyle = LegendStyle.Column;
		return specification;
	}

	internal static ChartSpecification BuildLogarithmic()
	{
		var specification = new ChartSpecification
		{
			YAxisIsLogarithmic = true,
			YAxisMajorGridEnabled = true,
			YAxisMinorGridEnabled = true,
			XAxisTitle = "Day",
			YAxisTitle = "Requests",
			UseYAxisShortLabels = true,
			SeriesList =
			[
				new()
				{
					ChartType = SeriesChartType.Line,
					LegendText = "Requests",
					StrokeColor = Color.IndianRed,
					StrokeWidth = 3,
					IsXValueIndexed = true,
					MarkerStyle = MarkerStyle.Circle,
					MarkerFillColor = Color.White,
					MarkerStrokeColor = Color.IndianRed,
					MarkerSize = 4,
					Points = Points(1, 9, 80, 700, 5000, 900, 40)
				}
			]
		};

		return specification;
	}
}
