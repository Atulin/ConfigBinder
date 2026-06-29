// Required for record types and init-only setters when targeting netstandard2.0.
// The compiler emits references to this type; it must exist somewhere in the assembly.
#if NETSTANDARD2_0
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit { }
}
#endif