namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.typescript;

public class AssemblyContext
{
  /// <summary>
  /// The extenders that should be generated as part of this module.
  /// </summary>
  private readonly List<string> _extendersToGenerate = new();

  /// <summary>
  /// The name of the current assembly;
  /// </summary>
  public string AssemblyName { get; }

  public AssemblyContext(string assemblyName)
  {
    AssemblyName = assemblyName;
  }

  public void RegisterReferencedType(string assembly, string typeName) => ReferencedTypes.Add((assembly, typeName));
  public void AddExtenderToGenerate(string extenderName) => _extendersToGenerate.Add(extenderName);

  public IEnumerable<string> ExtendersToGenerate => _extendersToGenerate;
  public HashSet<(string assembly, string type)> ReferencedTypes { get; } = new();
}