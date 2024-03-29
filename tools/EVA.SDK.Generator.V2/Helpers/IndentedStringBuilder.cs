﻿using System.Text;

namespace EVA.SDK.Generator.V2.Helpers;

internal class IndentedStringBuilder
{
  private readonly int _indentSize;
  private int _indent;
  private readonly StringBuilder _builder = new();
  private const char Linebreak = '\n';
  private bool _writeIndent = true;

  internal IDisposable Indentation => new IndentationScope(this);
  internal IDisposable BracedIndentation => new BracedIndentationScope(this);

  internal IndentedStringBuilder(int indentSize)
  {
    _indentSize = indentSize;
  }

  private sealed class IndentationScope : IDisposable
  {
    private readonly IndentedStringBuilder _sb;

    internal IndentationScope(IndentedStringBuilder sb)
    {
      _sb = sb;
      _sb._indent++;
    }

    public void Dispose() => _sb._indent--;
  }

  private sealed class BracedIndentationScope : IDisposable
  {
    private readonly IndentedStringBuilder _sb;

    internal BracedIndentationScope(IndentedStringBuilder sb)
    {
      _sb = sb;
      _sb.WriteLine("{");
      _sb._indent++;
    }

    public void Dispose()
    {
      _sb._indent--;
      _sb.WriteLine("}");
    }
  }

  private void WriteIndent() => _builder.Append(new string(' ', _indent * _indentSize));

  internal void WriteLine()
  {
    _builder.Append(Linebreak);
    _writeIndent = true;
  }

  internal void WriteLine(string s)
  {
    if(_writeIndent) WriteIndent();
    _builder.Append(s);
    _builder.Append(Linebreak);
    _writeIndent = true;
  }

  internal void Write(string s)
  {
    if(_writeIndent) WriteIndent();
    _builder.Append(s);
    _writeIndent = false;
  }

  internal void WriteLines(string s, string? prefix = null)
  {
    var lines = s.Split('\n');
    foreach(var l in lines) WriteLine((prefix ?? string.Empty) + l);
  }

  public override string ToString()
  {
    return _builder.ToString();
  }

  internal void WriteManifestResourceStream(string name)
  {
    var content = ManifestResourceHelpers.GetResource(name);
    if (content == null) return;
    WriteLines(content);
  }
}