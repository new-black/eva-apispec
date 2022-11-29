namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.typescript;

public class AssemblyContext
{
  /// <summary>
  /// All modules that are referenced from this one.
  /// </summary>
  private readonly HashSet<string> _referencedModules = new();

  /// <summary>
  /// The extenders that should be generated as part of this module.
  /// </summary>
  private List<string> _extendersToGenerate = new();

  /// <summary>
  /// The name of the current assembly;
  /// </summary>
  public string AssemblyName { get; init; }

  public void RegisterReferencedModule(string assembly)
  {
    if(assembly != AssemblyName) _referencedModules.Add(assembly);
  }

  public void AddExtenderToGenerate(string extenderName) => _extendersToGenerate.Add(extenderName);

  public IEnumerable<string> ReferencedModules => _referencedModules;
  public IEnumerable<string> ExtendersToGenerate => _extendersToGenerate;
}