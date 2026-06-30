using ConfigBinder.Helpers;

namespace ConfigBinder;

internal sealed record ConfigModel(
	string FullyQualifiedName,
	string Namespace,
	string Visibility,
	string TypeName,
	string SectionName,
	EquatableReadOnlyList<string> Properties,
	EquatableReadOnlyList<string> RequiredProperties)
{
	public string SafeName => FullyQualifiedName
		.Replace("global::", "")
		.Replace('.', '_')
		.Replace('<', '_')
		.Replace('>', '_')
		.Replace(',', '_')
		.Replace(' ', '_');
}