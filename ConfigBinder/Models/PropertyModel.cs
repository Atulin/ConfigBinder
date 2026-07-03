namespace ConfigBinder.Models;

internal enum ParseKind
{
	String,
	Bool,
	Byte,
	SByte,
	Short,
	UShort,
	Int,
	UInt,
	Long,
	ULong,
	Float,
	Double,
	Decimal,
	Char,
	Enum,
	Parsable,
	Dictionary,
}

internal sealed record PropertyModel(
	string Name,
	string KeyName,
	string TypeFqn,
	bool IsNullable,
	bool IsRequired,
	ParseKind ParseKind,
	PropConverterRef? PerPropConverter,
	ParseKind? DictValueTypeParseKind = null,
	string? DictValueTypeFqn = null);