using System.Drawing;
using System.Globalization;

namespace PanoramicData.ChartMagic.Demo.Services;

public static partial class SpecificationEditor
{
	private static bool TryParse(Type target, string? text, out object? parsed)
	{
		var underlying = Nullable.GetUnderlyingType(target) ?? target;
		if (string.IsNullOrWhiteSpace(text))
		{
			return TryParseEmpty(target, underlying, out parsed);
		}

		return TryParseNonEmpty(underlying, text.Trim(), out parsed);
	}

	private static bool TryParseEmpty(Type target, Type underlying, out object? parsed)
	{
		parsed = null;
		if (Nullable.GetUnderlyingType(target) is null && target.IsValueType)
		{
			return false;
		}

		parsed = underlying == typeof(string) ? string.Empty : null;
		return true;
	}

	private static bool TryParseNonEmpty(Type type, string text, out object? parsed)
	{
		if (type == typeof(string) || type == typeof(object))
		{
			parsed = text;
			return true;
		}

		if (type.IsEnum)
		{
			return Enum.TryParse(type, text, ignoreCase: true, out parsed);
		}

		if (type == typeof(Color))
		{
			return TryParseColour(text, out parsed);
		}

		if (type == typeof(bool))
		{
			return TryParseBoolean(text, out parsed);
		}

		return TryParseNumber(type, text, out parsed);
	}

	private static bool TryParseBoolean(string text, out object? parsed)
	{
		var succeeded = bool.TryParse(text, out var value);
		parsed = succeeded ? value : null;
		return succeeded;
	}

	private static bool TryParseNumber(Type type, string text, out object? parsed)
	{
		if (type == typeof(int) || type == typeof(long))
		{
			return TryParseWholeNumber(type, text, out parsed);
		}

		double number = 0;
		var supported = type == typeof(double) || type == typeof(float);
		var succeeded = supported
			&& double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
		parsed = succeeded ? type == typeof(double) ? number : (float)number : null;
		return succeeded;
	}

	private static bool TryParseWholeNumber(Type type, string text, out object? parsed)
	{
		var succeeded = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole);
		parsed = succeeded ? type == typeof(int) ? (int)whole : whole : null;
		return succeeded;
	}
}
