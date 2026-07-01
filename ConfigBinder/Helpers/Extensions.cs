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
}