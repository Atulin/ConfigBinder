using System.ComponentModel.DataAnnotations;
using ConfigBinder;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["SomeConfig:SomeString"] = "Hello World!";
builder.Configuration["SomeConfig:SomeInt"] = "42";

builder.Services.Replace(ServiceDescriptor.Singleton(() => new SomeConfig{ SomeString = "", SomeInt = 0}));

return;
	
[BindConfig("SomeConfig")]
internal sealed partial class SomeConfig
{
	[Required]
	public required string SomeString { get; init; }
	
	[Range(0, 100)]
	public required int SomeInt { get; init; }
}