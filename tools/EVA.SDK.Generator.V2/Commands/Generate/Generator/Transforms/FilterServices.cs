using System.Collections.Immutable;
using EVA.Core.Typings.V2;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Transforms;

public class FilterServices : ITransform
{
  private readonly HashSet<string> _services;

  public FilterServices(string[] services)
  {
    _services = services.ToHashSet();
  }

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options)
  {
    var count1 = input.Services.Length;
    input.Services = input.Services.Where(s => _services.Contains(s.Name)).ToImmutableArray();
    return input.Services.Length == count1 ? ITransform.TransformResult.NoChanges : ITransform.TransformResult.Changes;
  }
}