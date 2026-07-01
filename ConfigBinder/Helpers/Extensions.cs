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
		this ISymbol symbol, 
		string typeName, 
		string namespaceName, 
		StringComparison comparison = StringComparison.OrdinalIgnoreCase)
	{
		if (!symbol.Name.Equals(typeName, comparison))
		{
			return false;
		}
		if (!(symbol.ContainingNamespace?.ToDisplayString().Equals(namespaceName, comparison) ?? false))
		{
			return false;
		}
		return true;
	}
}