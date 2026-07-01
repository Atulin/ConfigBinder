namespace ConfigBinder.Sources;

public static class AttributeSources
{
	public const string AttributesNamespace = "ConfigBinder.Attributes";

	public const string ConfigRegistrationMode = // lang=cs
		$$"""
		  {{SharedSources.Header}}
		  #nullable enable

		  namespace {{AttributesNamespace}};

		  /// <summary>How the config type is exposed in the DI container.</summary>
		  public enum {{nameof(ConfigRegistrationMode)}}
		  {
		      /// <summary>
		      /// Bound instance registered as a plain singleton —
		      /// inject <c>T</c> directly, no IOptions wrapper.
		      /// </summary>
		      DirectAccess,

		      /// <summary>
		      /// Uses <c>AddOptions&lt;T&gt;().BindConfiguration(...).ValidateOnStart()</c>.
		      /// Inject via <c>IOptions&lt;T&gt;</c>, <c>IOptionsSnapshot&lt;T&gt;</c>,
		      /// or <c>IOptionsMonitor&lt;T&gt;</c>.
		      /// </summary>
		      Options,
		  }
		  """;

	public const string ConfigSectionAttribute = // lang=cs
		$$"""
		  {{SharedSources.Header}}
		  #nullable enable
		  
		  namespace {{AttributesNamespace}};
		
		  /// <summary>
		  /// Marks a class or record to be bound from a named configuration section.
		  /// </summary>
		  [global::System.AttributeUsage(
		      global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct,
		      Inherited = false)]
		  public sealed class {{nameof(ConfigSectionAttribute)}}(string sectionName) : global::System.Attribute
		  {
		      public string SectionName { get; } = sectionName;

		      /// <summary>
		      /// Override the registration mode for this type only.
		      /// When null, inherits from <see cref="ConfigSectionDefaultsAttribute"/> or
		      /// defaults to <see cref="ConfigRegistrationMode.DirectAccess"/>.
		      /// </summary>
		      public ConfigRegistrationMode Mode { get; set; } = ConfigRegistrationMode.DirectAccess;
		  }
		  """;
	
	public const string ConfigConverterAttribute = // lang=cs
		$$"""
		  {{SharedSources.Header}}
		  #nullable enable

		  namespace {{AttributesNamespace}};

		  /// <summary>
		  /// Points to a static converter method for a single property.
		  /// Method signature: <c>static T Convert(string? raw, string key)</c>
		  /// </summary>
		  [global::System.AttributeUsage(global::System.AttributeTargets.Property, Inherited = false)]
		  public sealed class {{nameof(ConfigConverterAttribute)}} (
		      global::System.Type converterType,
		      string methodName = "Convert") : global::System.Attribute
		  {
		      public global::System.Type ConverterType { get; } = converterType;
		      public string MethodName { get; } = methodName;
		  }
		  """;
	
	public const string ConfigTypeConverterAttribute = // lang=cs
		$$"""
		  {{SharedSources.Header}}
		  #nullable enable

		  namespace {{AttributesNamespace}};

		  /// <summary>
		  /// Assembly-level converter override for every property whose CLR type matches
		  /// <c>TargetType</c>. Overridden by per-property <see cref="ConfigConverterAttribute"/>.
		  /// Method signature: <c>static TTarget Convert(string? raw, string key)</c>
		  /// </summary>
		  [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = true)]
		  public sealed class {{nameof(ConfigTypeConverterAttribute)}}(
		      global::System.Type targetType,
		      global::System.Type converterType,
		      string methodName = "Convert") : global::System.Attribute
		  {
		      public global::System.Type TargetType    { get; } = targetType;
		      public global::System.Type ConverterType { get; } = converterType;
		      public string MethodName                 { get; } = methodName;
		  }
		  """;
	
	public const string ConfigSectionDefaultsAttribute = // lang=cs
		$$"""
		  {{SharedSources.Header}}
		  #nullable enable

		  namespace {{AttributesNamespace}};

		  /// <summary>
		  /// Sets assembly-wide defaults for all <see cref="ConfigSectionAttribute"/> usages.
		  /// </summary>
		  [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, Inherited = false)]
		  public sealed class {{nameof(ConfigSectionDefaultsAttribute)}} : global::System.Attribute
		  {
		      public ConfigRegistrationMode Mode { get; set; } = ConfigRegistrationMode.DirectAccess;
		  }
		  """;
}