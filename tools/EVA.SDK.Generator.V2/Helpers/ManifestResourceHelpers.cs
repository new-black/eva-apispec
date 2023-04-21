using System.Reflection;

namespace EVA.SDK.Generator.V2.Helpers;

public static class ManifestResourceHelpers
{
  public static string? GetResource(string name)
  {
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = assembly.GetManifestResourceNames().First(n => n.EndsWith(name));
    using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null) return null;
    using var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();
    return content;
  }
}