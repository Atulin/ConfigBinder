# ConfigBinder

[![NuGet](https://img.shields.io/nuget/v/Atulin.ConfigBinder.svg)](https://www.nuget.org/packages/Atulin.ConfigBinder)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**ConfigBinder** is a zero-reflection, highly performant configuration binding library for .NET, 
powered by Roslyn Source Generators. It allows you to bind strongly typed configuration classes directly from `IConfiguration` 
at compile time, eliminating the startup overhead and reflection cost associated with the built-in `Microsoft.Extensions.Configuration` binder.

## Features

- **Zero Reflection**: Uses Roslyn Source Generators to generate binding code at compile time.
- **AOT / Trimming Friendly**: Completely avoids reflection, making it perfect for Native AOT and heavily trimmed applications.
- **Direct Access or IOptions**: Choose between injecting your raw configuration objects directly (`AddSingleton<MyConfig>`) or using the standard `IOptions<MyConfig>` pattern.
- **Immediate.Validations Integration**: Out-of-the-box support for validating configurations on startup using `Immediate.Validations`.
- **Custom Converters**: Define property-level or global custom type converters for complex types.
- **Built-in Parsing**: Automatically parses all standard primitives, enums, and any type implementing `IParsable<T>`.

## Installation

Add the `ConfigBinder` package to your project. Since this is a source generator, you may want to reference it accordingly 
(though it provides attributes as well).

```xml
<PackageReference Include="ConfigBinder" Version="x.y.z" />
```

## Quick Start

### 1. Define your Configuration Model

Decorate your configuration class, struct, or record with `[ConfigSection("SectionName")]`. By default, 
properties must have an accessible setter (or `init`).

```csharp
using ConfigBinder.Attributes;

[ConfigSection("MyConfig")]
public sealed class MyConfig
{
    public required string Name { get; init; }
    public required int MaxRetries { get; init; }
}
```

### 2. Register Generated Configurations

In your `Program.cs` or startup code, call the generated extension method `RegisterGeneratedConfigs` on your `IServiceCollection`.

```csharp
var builder = WebApplication.CreateBuilder(args);

// This single call registers all types decorated with [ConfigSection]
builder.Services.RegisterGeneratedConfigs(builder.Configuration);

var app = builder.Build();

// You can now resolve your config!
var config = app.Services.GetRequiredService<IOptions<MyConfig>>();
```

## Registration Modes

ConfigBinder supports two modes for registering your configuration objects:

- `RegistrationMode.DirectAccess`: Registers the object directly as a singleton (`services.AddSingleton<T>`).
- `RegistrationMode.Options` (Default): Registers the object using the standard Options pattern (`services.AddOptions<T>()`).

You can override the mode on a per-class basis:

```csharp
[ConfigSection("MyConfig", Mode = ConfigRegistrationMode.DirectAccess)]
public class MyOptionsConfig { /* ... */ }
```

Or set an assembly-wide default:

```csharp
[assembly: ConfigSectionDefaults(Mode = ConfigRegistrationMode.DirectAccess)]
```

## Custom Converters

If you need to parse complex types that don't implement `IParsable<T>`, you can write custom converters. 
A converter is simply a type with a static method (default name `Convert`) taking a `string` and a `string` (property name) 
and returning the parsed type.

### Property-Level Converter

```csharp
[ConfigSection("Feature")]
public class FeatureConfig
{
    [ConfigConverter(typeof(MyCustomParser), "ParseMyType")]
    public MyType SomeProperty { get; set; }
}
```

### Global Converter

Register a converter for a specific type across your entire assembly:

```csharp
[assembly: ConfigTypeConverter(typeof(MyType), typeof(MyCustomParser))]
```

## Validation

If your project references [Immediate.Validations](https://github.com/ImmediatePlatform/Immediate.Validations) 
and your configuration type implements `IValidationTarget<T>`, `ConfigBinder` will automatically wire up `IValidateOptions<T>` 
when using `RegistrationMode.Options`. This ensures your configuration is strictly validated on application startup.

> [!WARNING]
> Validation works only in `RegistrationMode.Options` mode.

```csharp
using Immediate.Validations.Shared;

[Validate]
[ConfigSection("ValidatedConfig")]
public sealed partial class ValidatedConfig : IValidationTarget<ValidatedConfig>
{
    public required string Host { get; init; }
    public required int Port { get; init; }
}
```

## Example

A config model like

```csharp
[Validate]
[ConfigSection("Validated")]
internal sealed partial class ValidatedConfig : IValidationTarget<ValidatedConfig>
{
	[MinLength(10)]
	public required string Name { get; init; }
	public required float Weight { get; init; }
	public required DateTime BuildDate { get; init; }
}
```
will generate the following binding code:

```csharp
internal static class ValidatedConfigConfigBinder
{
	public static ValidatedConfig Bind(IConfiguration configuration)
	{
		var section = configuration.GetSection("Validated");

		var instance = new ValidatedConfig {
			Name = ValidateString(section["Name"], "Name"),
			Weight = ParseFloat(section["Weight"], "Weight"),
			BuildDate = ParseIParsable<DateTime>(section["BuildDate"], "BuildDate"),
		};

		return instance;
	}

	private static string ValidateString(string? value, string propertyName)
	{
		if (string.IsNullOrEmpty(value))
	    {
			throw Required(propertyName);
	    }
		return value;
	}
    
	private static T ParseIParsable<T>(string? value, string propertyName) where T : IParsable<T>
	{
		if (string.IsNullOrEmpty(value))
	    {
	        throw Required(propertyName);
	    }
	    if (T.TryParse(value, CultureInfo.InvariantCulture, out var t))
	    {
	     	return t;   
	    }
	    throw BadValue(propertyName, value, typeof(T).Name);
	}
    
	private static float ParseFloat(string? value, string propertyName)
	{
	    if (string.IsNullOrEmpty(value))
	    {
	        throw Required(propertyName);
	    }
	    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
	    {
	     	return n;   
	    }
	    throw BadValue(propertyName, value, "float");
	}
    
	private static InvalidOperationException Required(string key) =>
		new($"Required configuration key '{key}' is missing or empty");
	      
	private static InvalidOperationException BadValue(string key, string? value, string type) =>
		new($"Configuration key '{key}' value '{value}' cannot be parsed as '{type}'");
}
```
and the following extension method:

```csharp
public static class GeneratedConfigRegistration
{
	public static IServiceCollection RegisterGeneratedConfigs(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddSingleton<IOptionsFactory<ValidatedConfig>>(sp => 
			new ConfigBinderOptionsFactory<ValidatedConfig>(
				sp.GetRequiredService<IEnumerable<IConfigureOptions<ValidatedConfig>>>(),
				sp.GetRequiredService<IEnumerable<IPostConfigureOptions<ValidatedConfig>>>(),
				sp.GetRequiredService<IEnumerable<IValidateOptions<ValidatedConfig>>>(),
				_ => ValidatedConfigConfigBinder.Bind(configuration)));
		services.AddSingleton<IValidateOptions<ValidatedConfig>, ImmediateValidationOptionsValidator<ValidatedConfig>>();
		services.AddOptions<ValidatedConfig>().ValidateOnStart();

		return services;
	}
}
```
