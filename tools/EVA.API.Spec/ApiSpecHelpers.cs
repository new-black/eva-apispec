﻿namespace EVA.API.Spec;

public static class ApiSpecHelpers
{
  private static Dictionary<string, List<int>> DetermineActuallyUsedTypeArguments(ApiDefinitionModel apiDefinitionModel)
  {
    var result = new Dictionary<string, List<int>>();

    IEnumerable<int> DetermineUsedTypeArgument(string id, TypeSpecification? typeSpec = null)
    {
      if(result.TryGetValue(id, out var usedTypeArguments)) return usedTypeArguments;
      typeSpec ??= apiDefinitionModel.Types[id];

      var list = new List<int>();
      if (typeSpec.TypeArguments.IsEmpty) return Enumerable.Empty<int>();
      var ids = typeSpec.TypeArguments.Select((x, i) => (x, i)).ToDictionary(k => k.x, k => k.i);

      void Process(TypeReference typeReference)
      {
        // Ignore primitives
        if (ApiSpecConsts.AllPrimitives.Contains(typeReference.Name)) return;

        var name = typeReference.Name;
        if (char.IsUpper(name[0]) && typeReference.Arguments.IsEmpty)
        {
          // No-op
        }
        else if (char.IsUpper(name[0]) && !typeReference.Arguments.IsEmpty)
        {
          foreach(var i in DetermineUsedTypeArgument(name)) Process(typeReference.Arguments[i]);
        }
        else if (name == ApiSpecConsts.Specials.Map && typeReference.Arguments.Length == 2)
        {
          Process(typeReference.Arguments[0]);
          Process(typeReference.Arguments[1]);
        }
        else if (name == ApiSpecConsts.Specials.Array && typeReference.Arguments.Length == 1)
        {
          Process(typeReference.Arguments[0]);
        }
        else if (name == ApiSpecConsts.Specials.Option)
        {
          if(typeReference.Shared != null) Process(typeReference.Shared);
          foreach (var t in typeReference.Arguments) Process(t);
        }
        else if (name.StartsWith("_"))
        {
          list.Add(ids[typeReference.Name]);
        }
        else
        {
          throw new Exception();
        }
      }

      foreach (var prop in typeSpec.Properties.Values)
      {
        Process(prop.Type);
      }

      list = list.Distinct().ToList();
      result[id] = list;
      return list;
    }

    foreach (var (id, typeSpec) in apiDefinitionModel.Types)
    {
      DetermineUsedTypeArgument(id, typeSpec);
    }

    return result;
  }

  private static Dictionary<string, List<string>> DetermineActuallyUsedTypes(ApiDefinitionModel apiDefinitionModel)
  {
    var usedTypeArguments = DetermineActuallyUsedTypeArguments(apiDefinitionModel);
    var usedTypes = new Dictionary<string, List<string>>();

    foreach (var (id, typeSpec) in apiDefinitionModel.Types)
    {
      var list = usedTypes[id] = new List<string>();

      void Process(TypeReference typeReference)
      {
        // Ignore primitives
        if (ApiSpecConsts.AllPrimitives.Contains(typeReference.Name)) return;

        var name = typeReference.Name;
        if (char.IsUpper(name[0]) && typeReference.Arguments.IsEmpty)
        {
          list.Add(name);
        }
        else if (char.IsUpper(name[0]) && !typeReference.Arguments.IsEmpty)
        {
          list.Add(name);
          foreach (var i in usedTypeArguments.GetValueOrDefault(name) ?? Enumerable.Empty<int>()) Process(typeReference.Arguments[i]);
        }
        else if (name == ApiSpecConsts.Specials.Map && typeReference.Arguments.Length == 2)
        {
          Process(typeReference.Arguments[0]);
          Process(typeReference.Arguments[1]);
        }
        else if (name == ApiSpecConsts.Specials.Array && typeReference.Arguments.Length == 1)
        {
          Process(typeReference.Arguments[0]);
        }
        else if (name == ApiSpecConsts.Specials.Option)
        {
          if(typeReference.Shared != null) Process(typeReference.Shared);
          foreach (var t in typeReference.Arguments) Process(t);
        }
        else if (name.StartsWith("_"))
        {
          // No-op
        }
        else
        {
          throw new Exception();
        }
      }

      if (typeSpec.Extends != null) Process(typeSpec.Extends);
      foreach (var prop in typeSpec.Properties)
      {
        Process(prop.Value.Type);
      }
    }

    return usedTypes;
  }

  public static void RebuildTypeContext(ApiDefinitionModel apiDefinitionModel)
  {
    var actuallyUsedDependencies = DetermineActuallyUsedTypes(apiDefinitionModel);

    // Clear all usages
    foreach (var x in apiDefinitionModel.Types.Values) x.Usage = new TypeUsage { Request = false, Response = false };

    void ApplyRequest(string id)
    {
      var current = apiDefinitionModel.Types[id];
      if (current.Usage.Request) return;
      current.Usage.Request = true;

      foreach (var x in actuallyUsedDependencies.GetValueOrDefault(id) ?? Enumerable.Empty<string>())
      {
        ApplyRequest(x);
      }
    }
    void ApplyResponse(string id)
    {
      var current = apiDefinitionModel.Types[id];
      if (current.Usage.Response) return;
      current.Usage.Response = true;

      foreach(var x in actuallyUsedDependencies.GetValueOrDefault(id) ?? Enumerable.Empty<string>()) ApplyResponse(x);
    }

    foreach (var service in apiDefinitionModel.Services)
    {
      ApplyRequest(service.RequestTypeID);
      ApplyResponse(service.ResponseTypeID);
    }

    foreach (var error in apiDefinitionModel.Errors)
    {
      if (error.DataTypeID == null) continue;
      ApplyResponse(error.DataTypeID);
    }
  }
}