using System.Collections.Immutable;
using EVA.API.Spec;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms.Filters;

public class FilterServices : ITransform
{
  private readonly HashSet<string> _services;

  public FilterServices(string[] services)
  {
    _services = services.ToHashSet();
  }

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    var count1 = input.Services.Length;
    input.Services = input.Services.Where(s => _services.Contains(s.Name)).ToImmutableArray();
    return input.Services.Length == count1 ? ITransform.TransformResult.NoChanges : ITransform.TransformResult.Changes;
  }
}