using System.Reflection;
using System.Text;

namespace EVA.SDK.Generator.V2.Commands.Generate.Helpers;

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

  public void WriteLines(string s, string prefix = null)
  {
    var lines = s.Split('\n');
    foreach(var l in lines) WriteLine((prefix ?? string.Empty) + l);
  }

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

  public void WriteManifestResourceStream(string name)
  {
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = assembly.GetManifestResourceNames().First(n => n.EndsWith(name));
    using var stream = assembly.GetManifestResourceStream(resourceName);
    using var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();
    WriteLines(content);
  }
}