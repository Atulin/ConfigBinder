namespace ConfigBinder.Models;

internal enum RegistrationMode
{
	DirectAccess, 
	Options,
}

internal sealed record AssemblyDefaults(RegistrationMode Mode);