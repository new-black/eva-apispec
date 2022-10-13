using System.Text;

namespace EVA.SDK.Generator.V2.Helpers;

public class IndentedStringBuilder
{
  private readonly int _indentSize;
  private int _indent = 0;
  private StringBuilder _builder = new StringBuilder();
  private readonly string _linebreak = "\n";

  public IndentedStringBuilder(int indentSize)
  {
    _indentSize = indentSize;
  }

  public void Indent() => _indent++;
  public void Outdent() => _indent--;

  public void WriteLine() => _builder.Append(_linebreak);
  public void WriteLine(string s) => _builder.Append(new string(' ', _indent*_indentSize) + s + _linebreak);
  public void Write(string s) => _builder.Append(new string(' ', _indent*_indentSize) + s);

  public void WriteIndentend(Action<IndentedStringBuilder> o)
  {
    _indent++;
    o(this);
    _indent--;
  }

  public override string ToString()
  {
    return _builder.ToString();
  }
}