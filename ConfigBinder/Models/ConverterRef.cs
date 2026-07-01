namespace ConfigBinder.Models;

internal sealed record ConverterRef(string TargetFqn, string ConverterTypeFqn, string MethodName);