using System.Reflection;
using System.Text;

namespace EVA.SDK.Generator.V2.Helpers;

public class IndentedStringBuilder
{
  private readonly int _indentSize;
  private int _indent = 0;
  private readonly StringBuilder _builder = new();
  private const string Linebreak = "\n";
  private bool _writeIndent = true;

  public IDisposable Indentation => new IndentationScope(this);

  public IndentedStringBuilder(int indentSize)
  {
    _indentSize = indentSize;
  }

  private sealed class IndentationScope : IDisposable
  {
    private readonly IndentedStringBuilder _sb;

    public IndentationScope(IndentedStringBuilder sb)
    {
      _sb = sb;
      _sb._indent++;
    }

    public void Dispose() => _sb._indent--;
  }

  private void WriteIndent() => _builder.Append(new string(' ', _indent * _indentSize));

  public void WriteLine()
  {
    _builder.Append(Linebreak);
    _writeIndent = true;
  }

  public void WriteLine(string s)
  {
    if(_writeIndent) WriteIndent();
    _builder.Append(s);
    _builder.Append(Linebreak);
    _writeIndent = true;
  }

  public void Write(string s)
  {
    if(_writeIndent) WriteIndent();
    _builder.Append(s);
    _writeIndent = false;
  }

  public void WriteLines(string s, string? prefix = null)
  {
    var lines = s.Split('\n');
    foreach(var l in lines) WriteLine((prefix ?? string.Empty) + l);
  }

  public void WriteIndented(Action<IndentedStringBuilder> o)
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
    if (stream == null) return;
    using var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();
    WriteLines(content);
  }
}