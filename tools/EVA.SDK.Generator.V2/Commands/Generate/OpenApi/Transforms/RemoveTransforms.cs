using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;

internal class RemoveDeprecatedProperties : IApiTransform
{
  public string ID => "remove-deprecated-properties";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    foreach (var type in api.AllTypes)
    {
      foreach (var property in type.Properties.Values.Where(p => p.Deprecated != null))
      {
        type.Schema.Properties!.Remove(property.Name);
        type.Schema.Required?.Remove(property.Name);
        changes = true;
      }
    }

    return changes;
  }
}

internal class RemoveDeprecatedServices : IApiTransform
{
  public string ID => "remove-deprecated-services";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var deprecated = api.Services.Where(s => s.Deprecated != null).ToList();
    foreach (var service in deprecated) api.RemoveService(service);
    return deprecated.Count > 0;
  }
}

internal class RemoveErrors : IApiTransform
{
  public string ID => "remove-errors";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    if (api.Errors.Count == 0) return false;

    api.Errors.Clear();
    return true;
  }
}

internal class RemoveNestedTypes : IApiTransform
{
  public string ID => "remove-nested-types";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    foreach (var type in api.AllTypes.Where(t => t.Parent != null))
    {
      type.Schema.RemoveExtension(ExtensionNames.Parent);
      changes = true;
    }

    return changes;
  }
}

/// <summary>
/// Removes base types when the entire chain of base types has no properties.
/// </summary>
internal class RemoveEmptyBaseTypes : IApiTransform
{
  public string ID => "remove-empty-base-types";

  public bool Transform(EvaApi api, GenerateOptions options, ILogger logger)
  {
    var changes = false;

    foreach (var type in api.AllTypes)
    {
      if (type.Extends == null || !ChainHasNoProperties(type.Extends, api)) continue;

      type.Schema.AllOf = null;
      changes = true;
    }

    return changes;
  }

  private static bool ChainHasNoProperties(TypeRef? reference, EvaApi api)
  {
    if (reference is not NamedRef named || api.TryGetType(named.TypeID) is not { } type) return true;
    if (type.Properties.Any()) return false;
    return ChainHasNoProperties(type.Extends, api);
  }
}
