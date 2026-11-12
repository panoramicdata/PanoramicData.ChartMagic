using PanoramicData.ChartMagic.Models;
using System.Text.Json;

namespace PanoramicData.ChartMagic.Demo.Services;

public static partial class DocMagicRequest
{
	private static void WriteSeries(Utf8JsonWriter writer, ChartSpecification specification)
	{
		writer.WriteStartArray("SeriesList");

		var index = 0;
		foreach (var series in specification.SeriesList)
		{
			index++;
			writer.WriteStartObject();
			writer.WriteNumber("ChartType", ToEndpointValue(series.ChartType));
			writer.WriteString("Name", series.LegendText is { Length: > 0 } ? series.LegendText : $"Series {index}");
			writer.WriteBoolean("IsXValueIndexed", series.IsXValueIndexed);
			writer.WriteNumber("BorderWidth", (int)Math.Round(series.StrokeWidth));

			// One colour there, two here. A filled series carries its identity in its fill and a
			// line in its stroke, so the fill wins where there is one.
			var colour = series.FillColor.A > 0 ? series.FillColor : series.StrokeColor;
			writer.WriteString("Color", FormatColour(colour));

			if (series.LabelText is not null)
			{
				writer.WriteString("LabelText", series.LabelText);
			}

			if (series.LegendText is not null)
			{
				writer.WriteString("LegendText", series.LegendText);
			}

			WritePoints(writer, series.Points);
			writer.WriteEndObject();
		}

		writer.WriteEndArray();
	}

	private static void WritePoints(Utf8JsonWriter writer, List<ChartPoint> points)
	{
		writer.WriteStartArray("Points");
		foreach (var point in points)
		{
			writer.WriteStartObject();
			if (point.XValueString is { Length: > 0 })
			{
				writer.WriteString("XValue", point.XValueString);
			}
			else
			{
				writer.WriteNumber("XValue", point.XValue);
			}

			WriteNamed(writer, "YValue", point.YValue);
			if (point.Color is { } pointColour)
			{
				writer.WriteString("Color", FormatColour(pointColour));
			}

			writer.WriteEndObject();
		}

		writer.WriteEndArray();
	}

	/// <summary>
	/// Marker settings, which are per series here and parallel lists on the chart there.
	/// </summary>
	private static void WriteMarkerLists(Utf8JsonWriter writer, ChartSpecification specification)
	{
		WriteMarkerStyles(writer, specification.SeriesList);
		WriteList(writer, "MarkerColors", specification.SeriesList, s => s.MarkerFillColor is { } c ? FormatColour(c) : null);
		WriteList(writer, "MarkerBorderColors", specification.SeriesList, s => s.MarkerStrokeColor is { } c ? FormatColour(c) : null);

		writer.WriteStartArray("MarkerSizes");
		foreach (var series in specification.SeriesList)
		{
			if (series.MarkerSize is { } size)
			{
				writer.WriteNumberValue((int)Math.Round(size));
			}
			else
			{
				writer.WriteNullValue();
			}
		}

		writer.WriteEndArray();

		writer.WriteStartArray("MarkerBorderWidths");
		foreach (var series in specification.SeriesList)
		{
			if (series.MarkerStrokeWidth is { } width)
			{
				writer.WriteNumberValue((int)Math.Round(width));
			}
			else
			{
				writer.WriteNullValue();
			}
		}

		writer.WriteEndArray();
	}

	private static void WriteList(
		Utf8JsonWriter writer,
		string name,
		List<SeriesSpecification> seriesList,
		Func<SeriesSpecification, string?> select)
	{
		writer.WriteStartArray(name);
		foreach (var series in seriesList)
		{
			var value = select(series);
			if (value is null)
			{
				writer.WriteNullValue();
			}
			else
			{
				writer.WriteStringValue(value);
			}
		}

		writer.WriteEndArray();
	}

	/// <summary>
	/// Marker styles, as the numbers the endpoint expects.
	/// </summary>
	private static void WriteMarkerStyles(Utf8JsonWriter writer, List<SeriesSpecification> seriesList)
	{
		writer.WriteStartArray("MarkerStyles");
		foreach (var series in seriesList)
		{
			writer.WriteNumberValue(ToEndpointValue(series.MarkerStyle));
		}

		writer.WriteEndArray();
	}
}
