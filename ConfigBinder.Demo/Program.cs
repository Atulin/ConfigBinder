using System.Text.Json;
using ConfigBinder;
using ConfigBinder.Attributes;
using Immediate.Validations.Shared;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["SomeConfig:SomeString"] = "Hello World!";
builder.Configuration["SomeConfig:SomeInt"] = "42";

builder.Services.Replace(ServiceDescriptor.Singleton(() => new SomeConfig{ SomeString = "", SomeInt = 0 }));

builder.Services.RegisterGeneratedConfigs(builder.Configuration);

var app = builder.Build();

var cfg = app.Services.GetRequiredService<SomeConfig>();
var val = app.Services.GetRequiredService<ValidatedConfig>();

Console.WriteLine(JsonSerializer.Serialize(cfg));
Console.WriteLine(JsonSerializer.Serialize(val));

return;
	
[ConfigSection("SomeConfig")]
internal sealed class SomeConfig
{
	public required string SomeString { get; init; }
	public required int SomeInt { get; init; }
}

[Validate]
[ConfigSection("Validated")]
internal sealed partial class ValidatedConfig : IValidationTarget<ValidatedConfig>
{
	public required string Name { get; init; }
	public required float Weight { get; init; }
	public required DateTime BuildDate { get; init; }
}