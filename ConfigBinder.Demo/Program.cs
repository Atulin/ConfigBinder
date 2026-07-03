using System.Text.Json;
using ConfigBinder;
using ConfigBinder.Attributes;
using Immediate.Validations.Shared;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["SomeConfig:SomeString"] = "Hello World!";
builder.Configuration["SomeConfig:SomeInt"] = "42";
builder.Configuration["Validated:Name"] = "Validated Config";
builder.Configuration["Validated:DeclaredWeight"] = "67.69";
builder.Configuration["Validated:BuildDate"] = "2026-07-01 04:07:11";
builder.Configuration["Validated:Values:Key1"] = "1";
builder.Configuration["Validated:Values:Key2"] = "2";
builder.Configuration["Validated:Values:Key3"] = "3";

builder.Services.RegisterGeneratedConfigs(builder.Configuration);

var app = builder.Build();

var cfg = app.Services.GetRequiredService<SomeConfig>();
var val = app.Services.GetRequiredService<IOptions<ValidatedConfig>>();

Console.WriteLine(JsonSerializer.Serialize(cfg));
Console.WriteLine(JsonSerializer.Serialize(val.Value));

return;
	
[ConfigSection("SomeConfig", Mode = ConfigRegistrationMode.DirectAccess)]
internal sealed class SomeConfig
{
	public required string SomeString { get; init; }
	public required int SomeInt { get; init; }
}

[Validate]
[ConfigSection("Validated")]
internal sealed partial class ValidatedConfig : IValidationTarget<ValidatedConfig>
{
	[MinLength(10)]
	public required string Name { get; init; }
	[ConfigKeyName("DeclaredWeight")]
	public required float Weight { get; init; }
	public required DateTime BuildDate { get; init; }
	public required Dictionary<string, int> Values { get; init; }
}