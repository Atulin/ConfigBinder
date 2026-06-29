using System.ComponentModel.DataAnnotations;
using ConfigBinder;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["SomeConfig:SomeString"] = "Hello World!";
builder.Configuration["SomeConfig:SomeInt"] = "42";


[BindConfig("SomeConfig")]
internal sealed class SomeConfig
{
	[Required]
	public required string SomeString { get; init; }
	
	[Range(0, 100)]
	public required int SomeInt { get; init; }
}