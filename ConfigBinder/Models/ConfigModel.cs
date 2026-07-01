using ConfigBinder.Helpers;

namespace ConfigBinder.Models;

internal sealed record ConfigModel(
	string? Namespace,
	string TypeName,
	string FullyQualifiedName,
	string SectionName,
	EquatableReadOnlyList<PropertyModel> Properties,
	bool ImplementsIValidationTarget,
	RegistrationMode? ModeOverride
)
{
	public string SafeName => FullyQualifiedName
		.Replace("global::", "")
		.Replace('.', '_')
		.Replace('<', '_')
		.Replace('>', '_')
		.Replace(',', '_')
		.Replace(' ', '_');
}