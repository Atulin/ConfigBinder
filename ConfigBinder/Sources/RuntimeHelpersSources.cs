namespace ConfigBinder.Sources;

public static class RuntimeHelpersSources
{
	public const string RuntimeHelpersNamespace = "ConfigBinder.RuntimeHelpers";

	public const string ConfigBinderOptionsFactory = // lang=cs
		$$"""
		  {{SharedSources.Header}}
		  #nullable enable

		  namespace {{RuntimeHelpersNamespace}};

		  internal sealed class {{nameof(ConfigBinderOptionsFactory)}}<[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : global::Microsoft.Extensions.Options.OptionsFactory<T>
		      where T : class
		  {
		  	private readonly global::System.Func<string, T> _factory;

		  	public ConfigBinderOptionsFactory(
		  		global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.Options.IConfigureOptions<T>> setups,
		  		global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.Options.IPostConfigureOptions<T>> postConfigures,
		  		global::System.Collections.Generic.IEnumerable<global::Microsoft.Extensions.Options.IValidateOptions<T>> validations,
		  		global::System.Func<string, T> factory)
		  		: base(setups, postConfigures, validations)
		  	{
		  		_factory = factory;	
		  	}
		  	
		  	protected override T CreateInstance(string name) => _factory(name);
		  }
		  """;

	public const string ImmediateValidationOptionsValidator = // lang=cs
		$$"""
		  {{SharedSources.Header}}
		  #nullable enable

		  namespace {{RuntimeHelpersNamespace}};
		     
		  internal sealed class {{nameof(ImmediateValidationOptionsValidator)}}<T> : global::Microsoft.Extensions.Options.IValidateOptions<T>
		      where T : class, global::Immediate.Validations.Shared.IValidationTarget<T>
		  {
		      public global::Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, T options)
		      {
		          var errors = T.Validate(options);

		          if (errors is not { Count: > 0 })
		          {
		              return global::Microsoft.Extensions.Options.ValidateOptionsResult.Skip;
		  		  }

		          var messages = global::System.Linq.Enumerable.Select(
		              errors,
		              static e => $"{e.PropertyName}: {e.ErrorMessage}");

		          return global::Microsoft.Extensions.Options.ValidateOptionsResult.Fail(messages);
		      }
		  }
		  """;
}