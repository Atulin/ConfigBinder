using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ConfigBinder.Helpers;

public static class Extensions
{
	public static IncrementalGeneratorPostInitializationContext RegisterStatic(
		this IncrementalGeneratorPostInitializationContext ctx, 
		string fileName, 
		string source)
	{
		ctx.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
		return ctx;
	}

	public static bool Equals(
		this ISymbol? symbol, 
		params ReadOnlySpan<string> name)
	{
		if (symbol is null)
		{
			return false;
		}

		return name switch
		{
			[.. var rest, var last] => symbol.Name == last && symbol.ContainingNamespace.Equals(rest),
			[] => symbol is INamespaceSymbol { IsGlobalNamespace: true }
		};
	}
}