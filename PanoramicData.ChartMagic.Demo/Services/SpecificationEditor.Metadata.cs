using System.Collections;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace PanoramicData.ChartMagic.Demo.Services;

public static partial class SpecificationEditor
{
	private static EditorKind KindOf(PropertyInfo property)
	{
		var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
		if (!property.CanWrite || (!type.IsEnum && !IsEditableScalar(type)))
		{
			return EditorKind.ReadOnly;
		}

		if (type.IsEnum)
		{
			return EditorKind.Enumeration;
		}

		return type.Name switch
		{
			nameof(Boolean) => EditorKind.Boolean,
			nameof(Color) => EditorKind.Colour,
			nameof(Int32) or nameof(Int64) or nameof(Double) or nameof(Single) => EditorKind.Number,
			_ => EditorKind.Text
		};
	}


	private static bool IsEditableScalar(Type type) => type == typeof(bool)
		|| type == typeof(Color)
		|| type == typeof(int)
		|| type == typeof(long)
		|| type == typeof(double)
		|| type == typeof(float)
		|| type == typeof(string)
		|| type == typeof(object);

	private static IReadOnlyList<string> OptionsOf(PropertyInfo property)
	{
		var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
		if (!type.IsEnum)
		{
			return [];
		}

		var names = Enum.GetNames(type).ToList();

		// A nullable enum can be cleared, so it needs an empty option to clear it to.
		if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
		{
			names.Insert(0, string.Empty);
		}

		return names;
	}

	/// <summary>
	/// Formats a value for an input box: round-trippable, so reading and writing it back is a
	/// no-op.
	/// </summary>
	private static string Format(object? value) => value switch
	{
		null => string.Empty,
		bool flag => flag ? "true" : "false",
		Color colour => FormatColour(colour),
		double number => number.ToString("0.######", CultureInfo.InvariantCulture),
		float number => number.ToString("0.######", CultureInfo.InvariantCulture),
		string text => text,
		IEnumerable list => FormattableString.Invariant($"[{list.Cast<object?>().Count()} items]"),
		_ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
	};
}
