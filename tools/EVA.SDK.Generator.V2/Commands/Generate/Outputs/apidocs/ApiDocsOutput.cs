using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

internal class ApiDocsOutput : IOutput<ApiDocsOptions>
{
    private class State
    {
        internal readonly Dictionary<string, (bool backendID, bool systemID)> SupportsBackendIDCache = new();
        internal readonly Dictionary<string, string> TypeMapping = new();
        internal int nextID = 1;

        internal void ResetForService()
        {
            TypeMapping.Clear();
            nextID = 1;
        }
    }

    public string? OutputPattern => null;

    public string[] ForcedTransformations => new[]
    {
        RemoveGenerics.ID, RemoveInheritance.ID, RemoveDataLakeExports.ID, RemoveEventExports.ID, RemoveErrors.ID
    };

    public async Task Write(OutputContext<ApiDocsOptions> ctx)
    {
        var state = new State();

        await GenerateSidebar(ctx);
        var content = ManifestResourceHelpers.GetResource("apidocs.Resources.index.md") ?? string.Empty;
        await ctx.Writer.WriteFileAsync("index.md", content.Replace("__VERSION__", $"2.0.{ctx.Input.ApiVersion}"));

        foreach (var service in ctx.Input.Services)
        {
            state.ResetForService();
            await GenerateService(state, service, ctx);
        }
    }

    private static async Task GenerateSidebar(OutputContext<ApiDocsOptions> ctx)
    {
        var entries = ctx.Input.Services
            .Select(service => new ServiceIndex.Entry
            {
                Name = service.Name,
                Method = service.Method ?? "POST"
            })
            .OrderBy(x => x.Name).ToImmutableArray();

        var sidebar = new ServiceIndex
        {
            Entries = entries
        };

        await ctx.Writer.WriteFileAsync("eva/index.json", JsonSerializer.Serialize(sidebar, JsonContext.Default.ServiceIndex));
    }

    private static async Task GenerateService(State state, ServiceModel service, OutputContext<ApiDocsOptions> ctx)
    {
        var result = new SingleService
        {
            Name = service.Name,
            Path = service.Path,
            Method = service.Method ?? "POST",
            Types = new Dictionary<string, List<TypeInfo>>()
        };

        var requestType = ctx.Input.Types[service.RequestTypeID];
        var supportsExternalID = SupportsExternalIdsMode(state, ctx.Input, service.RequestTypeID);

        result.Description = requestType.Description ?? $"The {service.Name} service";

        result.Deprecation = GetDeprecationMessage(service);

        result.RequestTypeID = BuildTypeInfo(state, result.Types, ctx, service.RequestTypeID);
        result.ResponseTypeID = BuildTypeInfo(state, result.Types, ctx, service.ResponseTypeID);

        result.AuthDescription = GetAuthDescription(service);

        result.Headers = GetHeaders(supportsExternalID);

        result.RequestSamples = GetRequestSamples(ctx, service.RequestTypeID, service.Name);

        result.ResponseSamples = GetResponseSamples(ctx, service.ResponseTypeID, service.Name);

        await ctx.Writer.WriteFileAsync($"eva/services/{service.Name}.json", JsonSerializer.Serialize(result, JsonContext.Indented.SingleService));
    }

    private static string GetDeprecationMessage(ServiceModel service)
    {
        if (service.Deprecated is { } deprecated)
        {
            return $"\n\n**Deprecated since {deprecated.Introduced}:** {deprecated.Comment}\n\n**Will be removed from the typings in {deprecated.Effective}**";
        }
        return null;
    }

    private static string GetAuthDescription(ServiceModel service)
    {
        if (service.AuthInformation.RequiredPermissions.All(p => p is { Functionality: null, Scope: null, UserTypes: null }))
        {
            return "This service does not require authentication.";
        }

        var authDescription = string.Empty;
        foreach (var permission in service.AuthInformation.RequiredPermissions)
        {
            authDescription += permission switch
            {
                { Functionality: null, Scope: null, UserTypes: > 0 } => $"Requires a user of type {MapUserTypes(permission.UserTypes.Value)}",
                { Functionality: not null, Scope: null, UserTypes: > 0 } => $"Requires a user of type {MapUserTypes(permission.UserTypes.Value)} with functionality `{permission.Functionality}`",
                { Functionality: not null, Scope: not null, UserTypes: > 0 } => $"Requires a user of type {MapUserTypes(permission.UserTypes.Value)} with functionality `{permission.Functionality}:{permission.Scope}`",
                { Functionality: not null, Scope: null } => $"Requires any user with functionality `{permission.Functionality}`",
                { Functionality: not null, Scope: not null } => $"Requires any user with functionality `{permission.Functionality}:{permission.Scope}`",
                { Functionality: null, Scope: null } => "This service does not require authentication",
                _ => "> ???"
            };
        }

        return authDescription;
    }

    private static List<Header> GetHeaders((bool backendID, bool systemID) supportsExternalID)
    {
        var headers = new List<Header>
        {
            new Header
            {
                Name = "EVA-User-Agent",
                Type = "string",
                Description = "The user agent that is making these calls. Don't make this specific per device/browser but per application. This should be of the form: `MyFirstUserAgent/1.0.0`",
                DefaultValue = null,
                Required = true
            },
            new Header
            {
                Name = "EVA-Requested-OrganizationUnitID",
                Type = "integer",
                Description = "The ID of the organization unit to run this request in.",
                DefaultValue = null,
                Required = false
            },
            new Header
            {
                Name = "EVA-Requested-OrganizationUnit-Query",
                Type = "string",
                Description = "The query that selects the organization unit to run this request in.",
                DefaultValue = null,
                Required = false
            }
        };

        if (supportsExternalID.backendID)
        {
            headers.Add(new Header
            {
                Name = "EVA-IDs-Mode",
                Type = "string",
                Description = "The IDs mode to run this request in. Currently only `ExternalIDs` is supported.",
                DefaultValue = null,
                Required = false
            });
        }

        if (supportsExternalID.systemID)
        {
            headers.Add(new Header
            {
                Name = "EVA-IDs-BackendSystemID",
                Type = "string",
                Description = "The ID of the backend system that is used to resolve the IDs.",
                DefaultValue = null,
                Required = false
            });
        }

        return headers;
    }

    private static List<RequestSample> GetRequestSamples(OutputContext<ApiDocsOptions> ctx, string serviceRequestType, string serviceName)
    {
      var sample = BuildMinimalSample(ctx, serviceRequestType, new Stack<string>());
      var sampleStr = sample.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

      return new List<RequestSample>
      {
        new RequestSample
        {
          Name = "JSON",
          Sample = sample.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
          Syntax = "json"
        },
        new RequestSample
        {
          Name = "CURL",
          Sample = $"""
                   curl -H "Content-Type: application/json" \
                     -H "EVA-User-Agent: MyFirstUserAgent/1.0.0" \
                     -H "Authorization: Bearer <token>" \
                     --data '{sampleStr}' \
                     https://euw.acme.test.eva-online.cloud/message/{serviceName}
                   """,
          Syntax = "bash"
        }
      };
    }

    private static List<ResponseSample> GetResponseSamples(OutputContext<ApiDocsOptions> ctx, string serviceRequestType, string serviceName)
    {
        var sample = BuildMinimalSample(ctx, serviceRequestType, new Stack<string>());

        // Sample is awesome, but contains some fields that are only filled in specific scenario's
        if (sample is JsonObject jo)
        {
          jo.Remove("Error");
          if (serviceName.EndsWith("_AsyncResult"))
          {
            var metadata = jo["Metadata"] as JsonObject;
            var propsToRemove = metadata.Select(m => m.Key).Where(k => k != "IsAsyncResultAvailable").ToList();
            foreach (var prop in propsToRemove)
            {
              metadata.Remove(prop);
            }
          }
          else
          {
            jo.Remove("Metadata");
          }
        }

        return new List<ResponseSample>
        {
            new ResponseSample
            {
                Name = "200",
                Sample = sample.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            },
            new ResponseSample
            {
                Name = "400",
                Sample = "{\n  \"Error\": {\n    \"Code\": \"COVFEFE\",\n    \"Type\": \"RequestValidationFailure\",\n    \"Message\": \"Validation of the request message failed: Field ABC has an invalid value for a Product.\",\n    \"RequestID\": \"576b62dd71894e3281a4d84951f44e70\"\n  }\n}"
            },
            new ResponseSample
            {
                Name = "403",
                Sample = "{\n  \"Error\": {\n    \"Code\": \"COVFEFE\",\n    \"Type\": \"Forbidden\",\n    \"Message\": \"You are not authorized to execute this request.\",\n    \"RequestID\": \"576b62dd71894e3281a4d84951f44e70\"\n  }\n}"
            }
        };
    }

    private static JsonNode BuildMinimalSample(OutputContext<ApiDocsOptions> ctx, string typeID, Stack<string> recursionGuard)
    {
      var type = ctx.Input.Types[typeID];

      // Enums
      if (type.EnumIsFlag.HasValue)
      {
        // We'll just choose the first one
        return type.EnumValues.FirstOrDefault(x => x.Value.Value != 0).Value?.Value ?? 0;
      }

      // Object
      var result = new JsonObject();

      if (recursionGuard.Contains(typeID))
      {
        return new JsonObject
        {
          ["re"] = "cursion"
        };
      }
      recursionGuard.Push(typeID);

      foreach (var property in type.Properties)
      {
        result[property.Key] = BuildMinimalSample(ctx, property.Value, property.Value.Type, recursionGuard);
      }

      recursionGuard.Pop();

      return result;
    }

    private static JsonNode BuildMinimalSample(OutputContext<ApiDocsOptions> ctx, PropertySpecification spec, TypeReference type, Stack<string> recursionGuard)
    {
      if (type.Name is ApiSpecConsts.Bool)
      {
        return true;
      }

      if (type.Name is ApiSpecConsts.Int16 or ApiSpecConsts.Int32 or ApiSpecConsts.Int64 or ApiSpecConsts.ID)
      {
        return 123;
      }

      if (type.Name is ApiSpecConsts.Float32 or ApiSpecConsts.Float64 or ApiSpecConsts.Float128)
      {
        return 123.456f;
      }

      if (type.Name is ApiSpecConsts.Guid)
      {
        return Guid.NewGuid().ToString();
      }

      if (type.Name is ApiSpecConsts.Date)
      {
        return DateTime.UtcNow.ToString("yyyy-MM-dd");
      }

      if (type.Name is ApiSpecConsts.Duration)
      {
        return TimeSpan.FromHours(1).ToString();
      }

      if (type.Name is ApiSpecConsts.Any)
      {
        return "any valid json value";
      }

      if (type.Name is ApiSpecConsts.String)
      {
        if (spec.AllowedValues.Any())
        {
          return spec.AllowedValues.First();
        }

        var val = "string";
        if (spec.StringLengthConstraint is { Max: < 6 })
        {
          return val[..spec.StringLengthConstraint.Max];
        }

        return val;
      }

      if (type.Name is ApiSpecConsts.Binary)
      {
        return "base64 encoded binary data";
      }

      if (type.Name is ApiSpecConsts.Specials.Map)
      {
        var result = new JsonObject();
        result["key"] = BuildMinimalSample(ctx, spec, type.Arguments[1], recursionGuard);
        return result;
      }

      if (type.Name is ApiSpecConsts.Specials.Option)
      {
        return BuildMinimalSample(ctx, spec, type.Arguments[0], recursionGuard);
      }

      if (type.Name is ApiSpecConsts.Specials.Array)
      {
        return new JsonArray { BuildMinimalSample(ctx, spec, type.Arguments[0], recursionGuard) };
      }

      if (type.Name is ApiSpecConsts.Object)
      {
        var result = new JsonObject();
        result["key"] = "any valid json value";
        return result;
      }

      if (type.Name is { } typeName)
      {
        return BuildMinimalSample(ctx, typeName, recursionGuard);
      }

      return "unknown";
    }

    private static string BuildTypeInfo(State state, Dictionary<string, List<TypeInfo>> types, OutputContext<ApiDocsOptions> ctx, string typeID)
    {
        if (state.TypeMapping.TryGetValue(typeID, out var mapped))
        {
            return mapped;
        }

        var id = ToBase26(state.nextID++);
        state.TypeMapping[typeID] = id;

        var type = ctx.Input.Types[typeID];
        var result = new List<TypeInfo>();

        foreach (var (propname, prop) in type.Properties)
        {
            var ti = new TypeInfo
            {
                Name = propname,
                Required = !prop.Skippable && (prop.Required?.CurrentValue ?? false),
                Type = "unknown",
                Description = prop.Description ?? string.Empty
            };

            (ti.Type, ti.PropertiesID, var options, var extraDescription) = ToType(state, types, ctx, prop.Type);
            if(extraDescription != null) ti.Description += "\n" + extraDescription;
            ti.OneOf = options?.ToArray();
            result.Add(ti);

            if (prop.DataModelInformation is { Name: var dmn })
            {
              ti.Description += $"\n\nThis is the ID of a `{dmn}`";
            }

            if (prop.StringLengthConstraint is { } slc)
            {
              ti.Description += $"\n\nThis string must be between {slc.Min} (incl) and {slc.Max} (incl) characters long.";
            }

            if (prop.StringRegexConstraint is { } src)
            {
              ti.Description += $"\n\nThis string must be formatted like `{src.Regex}`.";
            }

            if (prop is { Skippable: true, Required: { CurrentValue: true } })
            {
              ti.Description += $"\n\nWhile this property is not required, if it is sent in the request it must have a valid value.";
            }
            else if (prop is { Skippable: true, Type.Nullable: true })
            {
              ti.Description += $"\n\nProviding a `null` value and not providing the property at all has different meanings.";
            }

            if (prop.AllowedValues is { Length: > 0 } allowedValues)
            {
              ti.Description += $"\n\nPossible values:\n\n{string.Join('\n', allowedValues.Select(v => $"* `{v}`"))}";
            }

            if (prop.Required is { Effective: not null } required)
            {
              ti.Description += $"\n\n**Required since {required.Introduced}:** {required.Comment}\n\n**Will be enforced in {required.Effective}**";
            }

            if (prop.Deprecated is { } deprecated)
            {
              ti.Deprecation = $"\n\n**Deprecated since {deprecated.Introduced}:** {deprecated.Comment}\n\n**Will be removed in {deprecated.Effective}**";
              ti.Description += $"\n\n**Deprecated since {deprecated.Introduced}:** {deprecated.Comment}\n\n**Will be removed in {deprecated.Effective}**";
            }

            ti.Description = ti.Description.Trim();
        }

        types[id] = result;

        return id;
    }

    private static (string type, string? properties, List<OneOf>? oneof, string? description) ToType(State state, Dictionary<string, List<TypeInfo>> types, OutputContext<ApiDocsOptions> ctx, TypeReference type,
        bool? forceNullable = null)
    {
        string? properties = null;
        List<OneOf>? oneof = null;
        string? description = null;

        var result = type.Name switch
        {
            ApiSpecConsts.Bool => "boolean",
            ApiSpecConsts.Int16 or ApiSpecConsts.Int32 or ApiSpecConsts.Int64 => "integer",
            ApiSpecConsts.Float32 or ApiSpecConsts.Float64 or ApiSpecConsts.Float128 => "float",
            ApiSpecConsts.Guid => "guid",
            ApiSpecConsts.Date => "datetime",
            ApiSpecConsts.String => "string",
            ApiSpecConsts.Binary => "binary",
            ApiSpecConsts.Any => "any",
            ApiSpecConsts.Duration => "duration",
            ApiSpecConsts.Object => "object",
            _ => null
        };

        if (type.Name == ApiSpecConsts.Specials.Map)
        {
            var (s1, s2, _, s3) = ToType(state, types, ctx, type.Arguments[1], false);

            result = $"map[{s1}]";
            properties = s2;
            description = s3;
        }
        else if (type.Name == ApiSpecConsts.Specials.Option)
        {
            var (s1, s2, _, s3) = ToType(state, types, ctx, type.Shared!, false);

            result = $"one-of[{s1}]";
            properties = s2;
            description = s3;

            oneof = new List<OneOf>();
            foreach (var x in type.Arguments)
            {
                var (_, x2, _, _) = ToType(state, types, ctx, x, false);
                oneof.Add(new OneOf { Name = x.Name[(x.Name.LastIndexOf('.') + 1)..], PropertiesID = x2 });
            }
        }
        else if (type.Name == ApiSpecConsts.Specials.Array)
        {
            var (s1, s2, _, s3) = ToType(state, types, ctx, type.Arguments[0], false);

            result = $"array[{s1}]";
            properties = s2;
            description = s3;
        }
        else if (result == null)
        {
          var targetType = ctx.Input.Types[type.Name];
          if (targetType.EnumIsFlag is {} enumIsFlag)
          {

            var totals = targetType.EnumValues.ToTotals();
            var possibleValues = string.Join('\n', totals.OrderBy(kv => kv.Value).Select(kv => $"* `{kv.Value}` - {kv.Key}"));

            if (enumIsFlag)
            {
              result = "integer";
              description = $"Flags enum, combine any of the below values:\n\n{possibleValues}";
            }
            else
            {
              result = "integer";
              description = $"Possible values:\n\n{possibleValues}";
            }
          }
          else
          {
            result = "object";
            properties = BuildTypeInfo(state, types, ctx, type.Name);
          }
        }

        if (forceNullable is true || !forceNullable.HasValue && type.Nullable)
        {
            result += " | null";
        }

        return (result, properties, oneof, description);
    }

    private static string ToBase26(int x)
    {
        var result = string.Empty;
        while (x > 0)
        {
            result = (char)('A' + x % 26) + result;
            x /= 26;
        }

        return result;
    }

    private static (bool backendID, bool systemID) SupportsExternalIdsMode(State state, ApiDefinitionModel input, string s)
    {
        if (state.SupportsBackendIDCache.TryGetValue(s, out var cached))
        {
            return cached;
        }

        var result = SupportsExternalIdsMode_Uncached(input, s, new HashSet<string>());
        state.SupportsBackendIDCache[s] = result;
        return result;
    }

    private static (bool backendID, bool systemID) SupportsExternalIdsMode_Uncached(ApiDefinitionModel input, string s, HashSet<string> recursionGuard)
    {
        if (!recursionGuard.Add(s)) return (false, false);

        var type = input.Types[s];

        var backendID = type.Properties.Any(p =>
            p.Value.DataModelInformation is { SupportsBackendID: true })
            || type.TypeDependencies.Any(d => SupportsExternalIdsMode_Uncached(input, d, recursionGuard).backendID);

        var systemID = type.Properties.Any(p =>
            p.Value.DataModelInformation is { SupportsSystemID: true })
            || type.TypeDependencies.Any(d => SupportsExternalIdsMode_Uncached(input, d, recursionGuard).systemID);

        recursionGuard.Remove(s);
        return (backendID, systemID);
    }

    private static string MapUserTypes(ApiSpecUserTypes t)
    {
        var values = Enum.GetValues<ApiSpecUserTypes>()
            .Where(v => v != ApiSpecUserTypes.None && t.HasFlag(v))
            .ToArray();

        if (values.Length == 0) return string.Empty;

        var str = $"`{values[0]}`";
        for (var i = 1; i < values.Length; i++)
        {
            str += $" or `{values[i]}`";
        }

        return str;
    }
}