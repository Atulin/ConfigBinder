using System;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace ConfigBinder.Helpers;

internal sealed class IndentedWriter(int initialCapacity = 4096)
{
	private readonly StringBuilder _sb = new(initialCapacity);
	private int _depth;
	private bool _atLineStart = true;
	private const string IndentUnit = "\t";

	// ── Core write primitives ──────────────────────────────────────────────

	/// Write raw text, respecting current indent level.
	public IndentedWriter Write(string text)
	{
		foreach (var ch in text)
		{
			if (_atLineStart && ch != '\n')
			{
				for (var i = 0; i < _depth; i++)
				{
					_sb.Append(IndentUnit);
				}
				_atLineStart = false;
			}
			_sb.Append(ch);
			if (ch == '\n')
			{
				_atLineStart = true;
			}
		}
		return this;
	}

	public IndentedWriter WriteLine(string text)
		=> Write(text).Write("\n");

	public IndentedWriter WriteLine()
		=> Write("\n");

	// ── Formatted convenience overloads ───────────────────────────────────
	// Using string.Format to stay netstandard2.0 compatible.

	public IndentedWriter WriteLine(string format, object? arg0)
		=> WriteLine(string.Format(format, arg0));

	public IndentedWriter WriteLine(string format, object? arg0, object? arg1)
		=> WriteLine(string.Format(format, arg0, arg1));

	public IndentedWriter WriteLine(string format, object? arg0, object? arg1, object? arg2)
		=> WriteLine(string.Format(format, arg0, arg1, arg2));

	// ── Block helpers ──────────────────────────────────────────────────────

	/// Opens a block with `open`, runs `body` indented, closes with `close`.
	public IndentedWriter Block(Action body, string open = "{", string close = "}")
	{
		WriteLine(open);
		Indent();
		body();
		Dedent();
		WriteLine(close);
		return this;
	}

	/// Block followed immediately by a semicolon — for object initialisers.
	public IndentedWriter InitBlock(Action body)
		=> Block(body, open: "{", close: "};");

	/// Block followed by nothing — for method bodies, class bodies, etc.
	public IndentedWriter BodyBlock(Action body)
		=> Block(body, open: "{", close: "}");

	// ── Indent control ─────────────────────────────────────────────────────

	public IndentedWriter Indent()
	{
		_depth++;
		return this;
	}

	public IndentedWriter Dedent()
	{
		_depth = Math.Max(0, _depth - 1);
		return this;
	}

	/// Temporarily increases indent for a lambda/scope without a brace block.
	public IndentedWriter Indented(Action body)
	{
		Indent();
		body();
		Dedent();
		return this;
	}

	public override string ToString() => _sb.ToString();
}