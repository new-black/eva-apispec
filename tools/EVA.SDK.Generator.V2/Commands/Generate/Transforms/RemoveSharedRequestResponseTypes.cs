using System.Collections.Immutable;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Transforms;

internal class RemoveSharedRequestResponseTypes : INamedTransform
{
  public const string ID = "remove-shared-req-res-types";
  public string Name => ID;
  public string Description => "Splits types that are used in both requests and responses into separate types";

  public ITransform.TransformResult Transform(ApiDefinitionModel input, GenerateOptions options, ILogger logger)
  {
    ApiSpecHelpers.RebuildTypeContext(input);

    var changes = ITransform.TransformResult.None;
    var renameCache = new Dictionary<string, string>();

    // Split all types into request and response types
    var newTypes = new Dictionary<string, TypeSpecification>(input.Types);
    foreach (var (typeID, typeSpec) in input.Types)
    {
      if (typeSpec.EnumIsFlag.HasValue || typeSpec.Usage is not { Request: true, Response: true }) continue;

      // Create a copy
      var responseTypeSpec = JsonContext.Default.TypeSpecification.Clone(typeSpec);
      typeSpec.Usage.Response = false;
      responseTypeSpec.Usage.Request = false;

      // Find new ID and name
      var responseTypeID = typeID;
      var addedResponses = 0;
      while(newTypes.ContainsKey(responseTypeID))
      {
        responseTypeID = AddResponse(responseTypeID);
        addedResponses++;
      }
      renameCache.Add(typeID, responseTypeID);

      for (var i = 0; i < addedResponses; i++) responseTypeSpec.TypeName = AddResponse(responseTypeSpec.TypeName);
      newTypes.Add(responseTypeID, responseTypeSpec);
    }

    if (!renameCache.Any()) return ITransform.TransformResult.None;
    input.Types = newTypes.ToImmutableSortedDictionary();

    // Update the referenced
    foreach (var (_,typeSpec) in input.Types)
    {
      if (!typeSpec.Usage.Response) continue;

      if(typeSpec.ParentType != null && renameCache.TryGetValue(typeSpec.ParentType, out var newParentType))
      {
        typeSpec.ParentType = newParentType;
        changes = ITransform.TransformResult.Changes;
      }

      foreach (var typeReference in typeSpec.EnumerateAllTypeReferences())
      {
        if (!renameCache.TryGetValue(typeReference.Name, out var rename)) continue;

        typeReference.Name = rename;
        changes = ITransform.TransformResult.Changes;
      }
    }

    return changes;
  }

  private static string AddResponse(string s)
  {
    var idx = s.IndexOf('`');
    if(idx == -1) return s + "Response";

    var idx2 = s.LastIndexOf('+');
    if(idx2 > idx) return s + "Response";

    return s[..idx] + "Response" + s[idx..];
  }
}