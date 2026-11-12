using PanoramicData.ChartMagic.Models;
using System.Reflection;

namespace PanoramicData.ChartMagic.Demo.Services;

/// <summary>
/// What kind of editor a property needs.
/// </summary>
public enum EditorKind
{
	/// <summary>A checkbox.</summary>
	Boolean,

	/// <summary>A dropdown of the enum's values.</summary>
	Enumeration,

	/// <summary>A number box.</summary>
	Number,

	/// <summary>A colour, edited as text so that named colours can be typed.</summary>
	Colour,

	/// <summary>A text box.</summary>
	Text,

	/// <summary>Shown but not editable: a collection, or a type with no sensible editor.</summary>
	ReadOnly
}

/// <summary>
/// One editable property of a specification.
/// </summary>
/// <param name="Name">The property name.</param>
/// <param name="Group">The heading it is filed under.</param>
/// <param name="Kind">The editor it needs.</param>
/// <param name="Options">The permitted values, for an enumeration.</param>
/// <param name="IsDefault">Whether it still holds the value a fresh specification would.</param>
public record PropertyEditor(
	string Name,
	string Group,
	EditorKind Kind,
	IReadOnlyList<string> Options,
	bool IsDefault);

/// <summary>
/// Reads and writes specification properties by name, so the demo can offer the whole
/// specification for editing without listing 130 properties by hand.
/// </summary>
/// <remarks>
/// Listing them by hand would go out of date the first time a property was added, and a demo
/// that silently omits a property is worse than one that shows it as unsupported.
/// </remarks>
public static partial class SpecificationEditor
{
	private static readonly PropertyInfo[] Properties = typeof(ChartSpecification)
		.GetProperties(BindingFlags.Public | BindingFlags.Instance)
		.Where(p => p.CanRead)
		.OrderBy(p => GroupOf(p.Name), StringComparer.Ordinal)
		.ThenBy(p => p.Name, StringComparer.Ordinal)
		.ToArray();

	/// <summary>
	/// Every property, in the order they should be shown.
	/// </summary>
	public static IReadOnlyList<PropertyEditor> Describe(ChartSpecification specification)
	{
		ArgumentNullException.ThrowIfNull(specification);

		var defaults = new ChartSpecification();
		return
		[
			.. Properties.Select(p => new PropertyEditor(
				p.Name,
				GroupOf(p.Name),
				KindOf(p),
				OptionsOf(p),
				Equals(Format(p.GetValue(specification)), Format(p.GetValue(defaults)))))
		];
	}

	/// <summary>
	/// The current value of a property, as text for an input box.
	/// </summary>
	public static string Read(ChartSpecification specification, string name)
	{
		ArgumentNullException.ThrowIfNull(specification);

		var property = Properties.FirstOrDefault(p => p.Name == name);
		return property is null ? string.Empty : Format(property.GetValue(specification));
	}

	/// <summary>
	/// Writes a property from text, and reports whether it took.
	/// </summary>
	/// <remarks>
	/// A value that will not parse is ignored rather than throwing: the user is typing into a
	/// live chart, and half-typed input is normal rather than exceptional.
	/// </remarks>
	public static bool Write(ChartSpecification specification, string name, string? value)
	{
		ArgumentNullException.ThrowIfNull(specification);

		var property = Properties.FirstOrDefault(p => p.Name == name);
		if (property is null || !property.CanWrite)
		{
			return false;
		}

		if (!TryParse(property.PropertyType, value, out var parsed))
		{
			return false;
		}

		property.SetValue(specification, parsed);
		return true;
	}

	/// <summary>
	/// A copy of a specification, so the theme colours can be applied to something disposable
	/// rather than to the specification the user is editing.
	/// </summary>
	/// <remarks>
	/// Points are records and are shared rather than copied: nothing mutates them, and copying
	/// them per render for every sample would be waste.
	/// </remarks>
	public static ChartSpecification Clone(ChartSpecification specification)
	{
		ArgumentNullException.ThrowIfNull(specification);

		var copy = new ChartSpecification();
		foreach (var property in Properties.Where(p => p.CanWrite))
		{
			property.SetValue(copy, property.GetValue(specification));
		}

		copy.SeriesList = [.. specification.SeriesList.Select(CloneSeries)];
		return copy;
	}

	private static SeriesSpecification CloneSeries(SeriesSpecification series)
	{
		var copy = new SeriesSpecification();
		foreach (var property in typeof(SeriesSpecification)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p is { CanRead: true, CanWrite: true }))
		{
			property.SetValue(copy, property.GetValue(series));
		}

		return copy;
	}

	/// <summary>
	/// Whether a property still holds the value a fresh specification would.
	/// </summary>
	public static bool IsDefault(ChartSpecification specification, string name)
	{
		ArgumentNullException.ThrowIfNull(specification);

		var property = Properties.FirstOrDefault(p => p.Name == name);
		return property is not null
			&& Equals(Format(property.GetValue(specification)), Format(property.GetValue(new ChartSpecification())));
	}

	/// <summary>
	/// Sets a property to a typed value, bypassing the text parsing.
	/// </summary>
	public static void WriteValue(ChartSpecification specification, string name, object value)
	{
		ArgumentNullException.ThrowIfNull(specification);

		Properties.FirstOrDefault(p => p.Name == name && p.CanWrite)?.SetValue(specification, value);
	}

	/// <summary>
	/// Puts one property back to the value a fresh specification would carry.
	/// </summary>
	public static bool ResetToDefault(ChartSpecification specification, string name)
	{
		ArgumentNullException.ThrowIfNull(specification);

		var property = Properties.FirstOrDefault(p => p.Name == name);
		if (property is null || !property.CanWrite)
		{
			return false;
		}

		property.SetValue(specification, property.GetValue(new ChartSpecification()));
		return true;
	}
}
