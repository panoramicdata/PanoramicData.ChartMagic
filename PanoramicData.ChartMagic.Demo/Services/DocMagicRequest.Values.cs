using System.Collections;
using System.Drawing;
using System.Globalization;
using System.Text.Json;

namespace PanoramicData.ChartMagic.Demo.Services;

public static partial class DocMagicRequest
{
	private static void WriteNamed(Utf8JsonWriter writer, string name, object? value)
	{
		switch (value)
		{
			case null:
				writer.WriteNull(name);
				break;

			case Color colour:
				writer.WriteString(name, FormatColour(colour));
				break;

			case bool flag:
				writer.WriteBoolean(name, flag);
				break;

			case string text:
				writer.WriteString(name, text);
				break;

			case Enum enumeration:
				writer.WriteNumber(name, ToEndpointValue(enumeration));
				break;

			case IEnumerable list:
				WriteArray(writer, name, list);
				break;

			default:
				writer.WriteNumber(name, Convert.ToDouble(value, CultureInfo.InvariantCulture));
				break;
		}
	}

	private static void WriteArray(Utf8JsonWriter writer, string name, IEnumerable items)
	{
		writer.WriteStartArray(name);
		foreach (var item in items)
		{
			switch (item)
			{
				case null:
					writer.WriteNullValue();
					break;
				case Color colour:
					writer.WriteStringValue(FormatColour(colour));
					break;
				case Enum enumeration:
					writer.WriteNumberValue(ToEndpointValue(enumeration));
					break;
				default:
					writer.WriteStringValue(Convert.ToString(item, CultureInfo.InvariantCulture));
					break;
			}
		}

		writer.WriteEndArray();
	}

	/// <summary>
	/// A colour as hex, one of the two forms the endpoint's colour converter reads.
	/// </summary>
	/// <remarks>
	/// Hex rather than the colour name, even where there is a name, so one code path covers every
	/// colour and alpha is never quietly dropped.
	/// </remarks>
	private static string FormatColour(Color colour)
		=> colour.A == 255
			? FormattableString.Invariant($"#{colour.R:X2}{colour.G:X2}{colour.B:X2}")
			: FormattableString.Invariant($"#{colour.A:X2}{colour.R:X2}{colour.G:X2}{colour.B:X2}");

	/// <summary>
	/// The endpoint's numeric value for an enumeration member of this library.
	/// </summary>
	/// <exception cref="NotSupportedException">
	/// The member has no counterpart in the tables. Thrown rather than defaulted: a comparison
	/// rendered from a wrong value is worse than none, because it reads as a rendering difference.
	/// </exception>
	private static int ToEndpointValue(Enum value)
	{
		var typeName = value.GetType().Name;
		if (EnumValues.TryGetValue(typeName, out var members)
			&& members.TryGetValue(value.ToString(), out var mapped))
		{
			return mapped;
		}

		throw new NotSupportedException(
			$"{typeName}.{value} has no known value on the DocMagic chart endpoint, so the comparison "
				+ "would render something else. Add it to EnumValues.");
	}
}
