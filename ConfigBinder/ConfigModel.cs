namespace ConfigBinder;

internal sealed record ConfigModel(
	string FullyQualifiedName,
	string TypeName,
	string SectionName
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