using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using ConfigBinder.Helpers;
using ConfigBinder.Models;
using ConfigBinder.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ConfigBinder;

[Generator(LanguageNames.CSharp)]
public sealed class BindConfigGenerator : IIncrementalGenerator
{
	private const string ConfigSectionAttrName = $"{AttributeSources.AttributesNamespace}.{nameof(AttributeSources.ConfigSectionAttribute)}";
	private const string TypeConverterAttrName = $"{AttributeSources.AttributesNamespace}.{nameof(AttributeSources.ConfigTypeConverterAttribute)}";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(static ctx => ctx
			.RegisterStatic($"{nameof(AttributeSources.ConfigRegistrationMode)}.g.cs", AttributeSources.ConfigRegistrationMode)
			.RegisterStatic($"{nameof(AttributeSources.ConfigSectionAttribute)}.g.cs", AttributeSources.ConfigSectionAttribute)
			.RegisterStatic($"{nameof(AttributeSources.ConfigKeyNameAttribute)}.g.cs", AttributeSources.ConfigKeyNameAttribute)
			.RegisterStatic($"{nameof(AttributeSources.ConfigSectionDefaultsAttribute)}.g.cs", AttributeSources.ConfigSectionDefaultsAttribute)
			.RegisterStatic($"{nameof(AttributeSources.ConfigConverterAttribute)}.g.cs", AttributeSources.ConfigConverterAttribute)
			.RegisterStatic($"{nameof(AttributeSources.ConfigTypeConverterAttribute)}.g.cs", AttributeSources.ConfigTypeConverterAttribute)
			.RegisterStatic($"{nameof(RuntimeHelpersSources.ConfigBinderOptionsFactory)}.g.cs", RuntimeHelpersSources.ConfigBinderOptionsFactory)
		);

		var assemblyDefaults =
			context.CompilationProvider.Select(static (c, _) => ReadAssemblyDefaults(c));

		var globalConverters =
			context.SyntaxProvider
				.ForAttributeWithMetadataName(
					TypeConverterAttrName,
					predicate: static (node, _) => node is CompilationUnitSyntax,
					transform: ReadGlobalTypeConverters
				)
				.Where(static m => m is not null)
				.Select(static (m, _) => m!)
				.Collect();

		var hasIv = context.CompilationProvider
			.Select(static (c, _) => c.GetTypeByMetadataName("Immediate.Validations.Shared.IValidationTarget`1") is not null);

		var models = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				ConfigSectionAttrName,
				predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax,
				transform: GetModel)
			.Where(static m => m is not null)
			.Select(static (m, _) => m!)
			.Collect();

		var combined = models
			.Combine(assemblyDefaults)
			.Combine(globalConverters)
			.Combine(hasIv);

		context.RegisterSourceOutput(combined, static (ctx, data) => {
			var (((models, defaults), globalConverters), hasIv) = data;
			Emit(ctx, models, defaults, globalConverters, hasIv);
		});
	}

	private static AssemblyDefaults ReadAssemblyDefaults(Compilation compilation)
	{
		var attr = compilation.Assembly
			.GetAttributes()
			.FirstOrDefault(a => a.AttributeClass.Equals(SharedSources.BaseNamespace, AttributeSources.Namespace, nameof(AttributeSources.ConfigSectionDefaultsAttribute)));

		var mode = RegistrationMode.Options;

		if (attr is null)
		{
			return new AssemblyDefaults(mode);
		}

		foreach (var named in attr.NamedArguments)
		{
			if (named.Key.Equals("Mode", StringComparison.OrdinalIgnoreCase) && named.Value.Value is int v)
			{
				mode = v is 1 ? RegistrationMode.Options : RegistrationMode.DirectAccess;
			}
		}

		return new AssemblyDefaults(mode);
	}

	private static ConverterRef? ReadGlobalTypeConverters(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
	{
		var attr = ctx.Attributes.FirstOrDefault();
		if (attr is null)
		{
			return null;
		}

		if (attr.ConstructorArguments.Length < 2)
		{
			return null;
		}

		var targetFqn = (attr.ConstructorArguments[0].Value as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var converterFqn = (attr.ConstructorArguments[1].Value as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var methodName = attr.ConstructorArguments.Length >= 3
			? attr.ConstructorArguments[2].Value as string ?? "Convert"
			: "Convert";

		if (targetFqn is not null && converterFqn is not null)
		{
			return new ConverterRef(targetFqn, converterFqn, methodName);
		}

		return null;
	}

	private static ConfigModel? GetModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();

		if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
		{
			return null;
		}

		if (ctx.Attributes.IsEmpty)
		{
			return null;
		}

		var attr = ctx.Attributes[0];

		if (attr.ConstructorArguments.IsEmpty)
		{
			return null;
		}

		if (attr.ConstructorArguments[0].Value is not string sectionName)
		{
			return null;
		}

		RegistrationMode? modeOverride = null;
		foreach (var named in attr.NamedArguments)
		{
			if (named.Key.Equals("Mode", StringComparison.OrdinalIgnoreCase) && named.Value.Value is int v)
			{
				modeOverride = v == 1 ? RegistrationMode.Options : RegistrationMode.DirectAccess;
			}
		}

		var implementsIvt = symbol.AllInterfaces
			.Any(i => i.OriginalDefinition.Equals("Immediate", "Validations", "Shared", "IValidationTarget"));

		var properties = new List<PropertyModel>();

		foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
		{
			ct.ThrowIfCancellationRequested();

			if (member.IsStatic || member.IsIndexer) continue;
			if (member.SetMethod?.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)) continue;

			PropConverterRef? propConverter = null;
			var converterAttr = member.GetAttributes()
				.FirstOrDefault(a => a.AttributeClass.Equals(SharedSources.BaseNamespace, AttributeSources.Namespace, nameof(AttributeSources.ConfigConverterAttribute)));

			if (converterAttr?.ConstructorArguments.Length >= 1)
			{
				var convType = (converterAttr.ConstructorArguments[0].Value as INamedTypeSymbol)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				var convMethod = (converterAttr.ConstructorArguments.Length >= 2)
					? converterAttr.ConstructorArguments[1].Value as string ?? "Convert"
					: "Convert";
				if (convType is not null)
				{
					propConverter = new PropConverterRef(convType, convMethod);
				}
			}

			var keyNameAttr = member
				.GetAttributes()
				.FirstOrDefault(a => a.AttributeClass.Equals(SharedSources.BaseNamespace, AttributeSources.Namespace, nameof(AttributeSources.ConfigKeyNameAttribute)));
			var keyName = keyNameAttr is { ConstructorArguments: [{ Value: string kn }] }
				? kn
				: member.Name;

			var propType = member.Type;

			var isDict = IsStringDictionaryType(propType, out var dictValueType);

			properties.Add(new PropertyModel(
				Name: member.Name,
				KeyName: keyName,
				TypeFqn: propType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				IsNullable: propType.NullableAnnotation == NullableAnnotation.Annotated,
				IsRequired: member.IsRequired,
				ParseKind: ClassifyParseKind(propType),
				PerPropConverter: propConverter,
				DictValueTypeFqn: isDict ? dictValueType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : null,
				DictValueTypeParseKind: isDict ? ClassifyParseKind(dictValueType!) : null
			));

		}

		return new ConfigModel(
			Namespace: symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
			TypeName: symbol.Name,
			FullyQualifiedName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			SectionName: sectionName,
			Properties: properties.ToEquatableReadOnlyList(),
			ImplementsIValidationTarget: implementsIvt,
			ModeOverride: modeOverride
		);

	}

	private static ParseKind ClassifyParseKind(ITypeSymbol type)
	{
		// Unwrap Nullable<T>
		if (type is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nt)
		{
			type = nt.TypeArguments[0];
		}

		return type.SpecialType switch
		{
			SpecialType.System_String => ParseKind.String,
			SpecialType.System_Boolean => ParseKind.Bool,
			SpecialType.System_Byte => ParseKind.Byte,
			SpecialType.System_SByte => ParseKind.SByte,
			SpecialType.System_Int16 => ParseKind.Short,
			SpecialType.System_UInt16 => ParseKind.UShort,
			SpecialType.System_Int32 => ParseKind.Int,
			SpecialType.System_UInt32 => ParseKind.UInt,
			SpecialType.System_Int64 => ParseKind.Long,
			SpecialType.System_UInt64 => ParseKind.ULong,
			SpecialType.System_Single => ParseKind.Float,
			SpecialType.System_Double => ParseKind.Double,
			SpecialType.System_Decimal => ParseKind.Decimal,
			SpecialType.System_Char => ParseKind.Char,
			_ when type.TypeKind == TypeKind.Enum => ParseKind.Enum,
			_ when IsStringDictionaryType(type, out _) => ParseKind.Dictionary,
			_ => ParseKind.Parsable,
		};
	}

	private static bool IsStringDictionaryType(ITypeSymbol type, [NotNullWhen(true)] out ITypeSymbol? valueType)
	{
		if (type is not INamedTypeSymbol { IsGenericType: true } named)
		{
			valueType = null;
			return false;
		}

		if (named.TypeArguments is [{ SpecialType: SpecialType.System_String }, var kt]
		    && (named.OriginalDefinition.Equals("System", "Collections", "Generic", "Dictionary")
		        || named.OriginalDefinition.Equals("System", "Collections", "Generic", "IDictionary")
		        || named.OriginalDefinition.Equals("System", "Collections", "Generic", "IReadOnlyDictionary"))
		   )
		{
			valueType = kt;
			return true;
		}

		valueType = null;
		return false;
	}

	private static void Emit(
		SourceProductionContext ctx,
		ImmutableArray<ConfigModel> models,
		AssemblyDefaults defaults,
		ImmutableArray<ConverterRef> globalConverters,
		bool hasIv)
	{
		if (models.IsEmpty)
		{
			return;
		}

		if (hasIv)
		{
			ctx.AddSource(
				$"{nameof(RuntimeHelpersSources.ImmediateValidationOptionsValidator)}.g.cs",
				SourceText.From(RuntimeHelpersSources.ImmediateValidationOptionsValidator, Encoding.UTF8));
		}

		foreach (var model in models)
		{
			ctx.CancellationToken.ThrowIfCancellationRequested();

			ctx.AddSource(
				$"ConfigBinder.{model.SafeName}.g.cs",
				SourceText.From(EmitBinder(model, globalConverters), Encoding.UTF8));
		}

		ctx.AddSource(
			"BindConfigRegistration.g.cs",
			SourceText.From(EmitRegistration(models, defaults), Encoding.UTF8));
	}

	private static string EmitBinder(
		ConfigModel model,
		ImmutableArray<ConverterRef> globalConverters)
	{
		var converters = globalConverters
			.GroupBy(x => x.TargetFqn)
			.ToImmutableDictionary(
				static g => g.Key,
				static g => {
					var first = g.First();
					return new PropConverterRef(first.ConverterTypeFqn, first.MethodName);
				},
				StringComparer.Ordinal);

		var w = new IndentedWriter();

		w.WriteLine(SharedSources.Header);
		w.WriteLine("#nullable enable");
		w.WriteLine();
		if (model.Namespace is not null)
		{
			w.WriteLine($"namespace {model.Namespace};");
			w.WriteLine();
		}
		w.WriteLine($"""// Reflection-free binder for {model.TypeName} ← section "{model.SectionName}".""");
		w.WriteLine($"internal static class {model.SafeName}ConfigBinder");
		w.BodyBlock(() => {
			w.WriteLine($"public static {model.FullyQualifiedName} Bind(global::Microsoft.Extensions.Configuration.IConfiguration configuration)");
			w.BodyBlock(() => {
				w.WriteLine($"""var section = configuration.GetSection("{model.SectionName}");""");
				w.WriteLine();
				w.Write($"var instance = new {model.FullyQualifiedName} ");
				w.InitBlock(() => {
					foreach (var prop in model.Properties)
					{
						EmitPropertyAssignment(w, prop, ResolveConverter(prop, converters));
					}
				});
				w.WriteLine();
				w.WriteLine("return instance;");
			});
			w.WriteLine();

			var kinds = model.Properties
				.Select(p => p.ParseKind)
				.ToImmutableHashSet();

			var dictKinds = model.Properties
				.Select(p => p.DictValueTypeParseKind)
				.OfType<ParseKind>()
				.ToImmutableHashSet();

			EmitParseHelpers(w, [..kinds, ..dictKinds]);

			foreach (var prop in model.Properties.Where(p => p is { DictValueTypeFqn: not null, DictValueTypeParseKind: not null }))
			{
				w.WriteLine();
				EmitDictionaryBuilder(w, prop);
			}
		});

		return w.ToString();
	}

	private static void EmitDictionaryBuilder(IndentedWriter w, PropertyModel prop)
	{
		if (prop.DictValueTypeFqn is null || prop.DictValueTypeParseKind is not {} dvtpk)
		{
			w.WriteLine($"// Dictionary builder not available for property {prop.Name} of type {prop.TypeFqn} (no dictionary value type) - skipping");
			return;
		}
		var parseCall = BuildParseCall(prop with { TypeFqn = prop.DictValueTypeFqn, ParseKind = dvtpk }, "entry.Value");

		w.Write("private static global::System.Collections.Generic.Dictionary<string, ");
		w.Write(prop.DictValueTypeFqn);
		w.WriteLine($"> Build{prop.Name}Dictionary(global::Microsoft.Extensions.Configuration.IConfigurationSection section)");
		w.BodyBlock(() => {
			w.WriteLine($"""var dictSection = section.GetSection("{prop.Name}");""");
			w.WriteLine($"var dict = new global::System.Collections.Generic.Dictionary<string, {prop.DictValueTypeFqn}>();");
			w.WriteLine("foreach (var entry in dictSection.GetChildren())");
			w.BodyBlock(() => {
				w.WriteLine($"dict[entry.Key] = {parseCall};");
			});
			w.WriteLine("return dict;");
		});
	}

	private static void EmitPropertyAssignment(IndentedWriter w, PropertyModel prop, PropConverterRef? converter)
	{
		var raw = $"""section["{prop.KeyName}"]""";

		w.WriteLine($"// {prop}");

		switch (prop)
		{
			case { IsNullable: true, IsRequired: false, DictValueTypeFqn: null }:
			{
				var guard = $"_v_{prop.Name}";
				w.WriteLine($"{prop.Name} = {raw} is string {guard} && !string.IsNullOrEmpty({guard})");
				w.Indented(() => {
					w.Write("? ");
					var call = converter is not null
						? $"""global::{converter.ConverterTypeFqn}.{converter.MethodName}({guard}, "{prop.Name}")"""
						: BuildParseCall(prop, guard);
					w.WriteLine(call);
					w.WriteLine(" : default,");
				});
				break;
			}
			case { DictValueTypeFqn: not null }:
			{
				w.Write($"{prop.Name} = ");
				var call = converter is not null
					? $"""global::{converter.ConverterTypeFqn}.{converter.MethodName}({raw}, "{prop.Name}")"""
					: $"Build{prop.Name}Dictionary(section)";
				w.WriteLine($"{call},");
				break;
			}
			default:
			{
				w.Write($"{prop.Name} = ");
				var call = converter is not null
					? $"""global::{converter.ConverterTypeFqn}.{converter.MethodName}({raw}, "{prop.Name}")"""
					: BuildParseCall(prop, raw);
				w.WriteLine($"{call},");
				break;
			}
		}
	}

	private static string BuildParseCall(PropertyModel prop, string varExpr) =>
		prop.ParseKind switch
		{
			ParseKind.String when prop.IsRequired || !prop.IsNullable => $"""ValidateString({varExpr}, "{prop.Name}")""",
			ParseKind.String => varExpr,
			ParseKind.Bool => $"""ParseBool({varExpr}, "{prop.Name}")""",
			ParseKind.Byte => $"""ParseByte({varExpr}, "{prop.Name}")""",
			ParseKind.SByte => $"""ParseSByte({varExpr}, "{prop.Name}")""",
			ParseKind.Short => $"""ParseShort({varExpr}, "{prop.Name}")""",
			ParseKind.UShort => $"""ParseUShort({varExpr}, "{prop.Name}")""",
			ParseKind.Int => $"""ParseInt({varExpr}, "{prop.Name}")""",
			ParseKind.UInt => $"""ParseUInt({varExpr}, "{prop.Name}")""",
			ParseKind.Long => $"""ParseLong({varExpr}, "{prop.Name}")""",
			ParseKind.ULong => $"""ParseULong({varExpr}, "{prop.Name}")""",
			ParseKind.Float => $"""ParseFloat({varExpr}, "{prop.Name}")""",
			ParseKind.Double => $"""ParseDouble({varExpr}, "{prop.Name}")""",
			ParseKind.Decimal => $"""ParseDecimal({varExpr}, "{prop.Name}")""",
			ParseKind.Char => $"""ParseChar({varExpr}, "{prop.Name}")""",
			ParseKind.Enum => $"""ParseEnum<{prop.TypeFqn}>({varExpr}, "{prop.Name}")""",
			_ => $"""ParseIParsable<{prop.TypeFqn}>({varExpr}, "{prop.Name}")""",
		};

	private static PropConverterRef? ResolveConverter(
		PropertyModel prop,
		ImmutableDictionary<string, PropConverterRef> globalConverters)
	{
		if (prop.PerPropConverter is not null)
		{
			return prop.PerPropConverter;
		}

		return globalConverters.TryGetValue(prop.TypeFqn, out var g)
			? g
			: null;
	}

	private static void EmitParseHelpers(IndentedWriter w, IEnumerable<ParseKind> kinds)
	{
		var set = new HashSet<ParseKind>(kinds);

		w.WriteLine("// Built-in reflection-free parser helpers");
		w.WriteLine();

		if (set.Contains(ParseKind.String))
		{
			w.WriteLine( // lang=cs
				"""
				private static string ValidateString(string? value, string propertyName)
				{
					if (string.IsNullOrEmpty(value))
				    {
						throw Required(propertyName);
				    }
					return value;
				}
				""");
			w.WriteLine();
		}
		if (set.Contains(ParseKind.Bool))
		{
			w.WriteLine( // lang=cs
				"""
				private static bool ParseBool(string? value, string propertyName)
				{
					if (string.IsNullOrEmpty(value))
				    {
				        throw Required(propertyName);
				    }
				    if (bool.TryParse(value, out var b))
				    {
				     	return b;   
				    }
				    throw BadValue(propertyName, value, "Boolean");
				}
				""");
			w.WriteLine();
		}
		if (set.Contains(ParseKind.Char))
		{
			w.WriteLine( // lang=cs
				"""
				private static char ParseChar(string? value, string propertyName)
				{
					if (string.IsNullOrEmpty(value))
				    {
				        throw Required(propertyName);
				    }
				    if (value.Length == 1)
				    {
				     	return value[0];   
				    }
				    throw BadValue(propertyName, value, "Char");
				}
				""");
			w.WriteLine();
		}
		if (set.Contains(ParseKind.Enum))
		{
			w.WriteLine( // lang=cs
				"""
				private static TEnum ParseEnum<TEnum>(string? value, string propertyName) where TEnum : struct, Enum
				{
					if (string.IsNullOrEmpty(value))
				    {
				        throw Required(propertyName);
				    }
				    if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var e))
				    {
				     	return e;   
				    }
				    throw BadValue(propertyName, value, typeof(TEnum).Name);
				}
				""");
			w.WriteLine();
		}
		if (set.Contains(ParseKind.Parsable))
		{
			w.WriteLine( // lang=cs
				"""
				private static T ParseIParsable<T>(string? value, string propertyName) where T : IParsable<T>
				{
					if (string.IsNullOrEmpty(value))
				    {
				        throw Required(propertyName);
				    }
				    if (T.TryParse(value, global::System.Globalization.CultureInfo.InvariantCulture, out var t))
				    {
				     	return t;   
				    }
				    throw BadValue(propertyName, value, typeof(T).Name);
				}
				""");
			w.WriteLine();
		}

		var numerics = new[]
		{
			(ParseKind.Byte, "byte", "Integer"),
			(ParseKind.SByte, "sbyte", "Integer"),
			(ParseKind.Short, "short", "Integer"),
			(ParseKind.UShort, "ushort", "Integer"),
			(ParseKind.Int, "int", "Integer"),
			(ParseKind.UInt, "uint", "Integer"),
			(ParseKind.Long, "long", "Integer"),
			(ParseKind.ULong, "ulong", "Integer"),
			(ParseKind.Float, "float", "Float"),
			(ParseKind.Double, "double", "Float"),
			(ParseKind.Decimal, "decimal", "Number"),
		};

		foreach (var (kind, name, style) in numerics)
		{
			if (!set.Contains(kind))
			{
				continue;
			}

			var capitalized = $"{char.ToUpper(name[0])}{name[1..]}";

			w.WriteLine( // lang=cs
				$$"""
				  private static {{name}} Parse{{capitalized}}(string? value, string propertyName)
				  {
				      if (string.IsNullOrEmpty(value))
				      {
				          throw Required(propertyName);
				      }
				      if ({{name}}.TryParse(value, global::System.Globalization.NumberStyles.{{style}}, global::System.Globalization.CultureInfo.InvariantCulture, out var n))
				      {
				       	return n;   
				      }
				      throw BadValue(propertyName, value, "{{name}}");
				  }
				  """);
			w.WriteLine();
		}

		// Shared error factories
		w.WriteLine( // lang=cs
			"""
			private static InvalidOperationException Required(string key) =>
				new($"Required configuration key '{key}' is missing or empty");
			      
			private static InvalidOperationException BadValue(string key, string? value, string type) =>
				new($"Configuration key '{key}' value '{value}' cannot be parsed as '{type}'");
			""");
	}

	private static string EmitRegistration(ImmutableArray<ConfigModel> models, AssemblyDefaults defaults)
	{
		var w = new IndentedWriter();

		w.WriteLine(SharedSources.Header);
		w.WriteLine("#nullable enable");
		w.WriteLine();
		w.WriteLine($"namespace {SharedSources.BaseNamespace};");
		w.WriteLine();
		w.WriteLine("public static class GeneratedConfigRegistration");
		w.BodyBlock(() => {
			w.WriteLine("public static IServiceCollection RegisterGeneratedConfigs(");
			w.Indented(() => {
				w.WriteLine("this IServiceCollection services,");
				w.WriteLine("IConfiguration configuration)");
			});
			w.BodyBlock(() => {
				foreach (var model in models)
				{
					var mode = model.ModeOverride ?? defaults.Mode;
					w.WriteLine($"""// {model.FullyQualifiedName} → "{model.SectionName}" [{mode}]""");

					if (mode == RegistrationMode.DirectAccess)
					{
						EmitDirectRegistration(w, model);
					}
					else
					{
						EmitOptionsRegistration(w, model);
					}
					w.WriteLine();
				}

				w.WriteLine("return services;");
			});
		});

		return w.ToString();
	}

	private static void EmitDirectRegistration(IndentedWriter w, ConfigModel model)
	{
		var binder = model.Namespace is not null
			? $"{model.Namespace}.{model.SafeName}ConfigBinder"
			: $"{model.SafeName}ConfigBinder";

		w.WriteLine($"services.AddSingleton({binder}.Bind(configuration));");
	}

	private static void EmitOptionsRegistration(IndentedWriter w, ConfigModel model)
	{
		var binder = model.Namespace is not null
			? $"{model.Namespace}.{model.SafeName}ConfigBinder"
			: $"{model.SafeName}ConfigBinder";

		w.WriteLine($"services.AddSingleton<global::Microsoft.Extensions.Options.IOptionsFactory<{model.FullyQualifiedName}>>(sp => ");
		w.Indented(() => {
			w.WriteLine($"new global::{RuntimeHelpersSources.RuntimeHelpersNamespace}.{nameof(RuntimeHelpersSources.ConfigBinderOptionsFactory)}<{model.FullyQualifiedName}>(");
			w.Indented(() => {
				w.WriteLine($"sp.GetRequiredService<global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.Options.IConfigureOptions<{model.FullyQualifiedName}>>>(),");
				w.WriteLine($"sp.GetRequiredService<global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.Options.IPostConfigureOptions<{model.FullyQualifiedName}>>>(),");
				w.WriteLine($"sp.GetRequiredService<global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.Options.IValidateOptions<{model.FullyQualifiedName}>>>(),");
				w.WriteLine($"_ => {binder}.Bind(configuration)));");
			});
		});

		if (model.ImplementsIValidationTarget)
		{
			w.Write($"services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{model.FullyQualifiedName}>, ");
			w.WriteLine($"global::{RuntimeHelpersSources.RuntimeHelpersNamespace}.{nameof(RuntimeHelpersSources.ImmediateValidationOptionsValidator)}<{model.FullyQualifiedName}>>();");
		}

		w.WriteLine($"services.AddOptions<{model.FullyQualifiedName}>().ValidateOnStart();");
	}
}