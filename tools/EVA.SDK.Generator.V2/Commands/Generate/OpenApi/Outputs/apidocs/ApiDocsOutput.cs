using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;
using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Outputs.apidocs;

internal class ApiDocsOutput : IApiOutput<ApiDocsOptions>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
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

    public bool GetForcedTransformations(ApiDocsOptions _, IApiTransform x) =>
      x is RemoveGenerics or RemoveInheritance or RemoveErrors;

    public async Task Write(ApiOutputContext<ApiDocsOptions> ctx)
    {
        var state = new State();

        await GenerateSidebar(ctx);
        var content = ManifestResourceHelpers.GetResource("apidocs.Resources.index.md") ?? string.Empty;
        await ctx.Writer.WriteFileAsync("index.md", content.Replace("__VERSION__", $"2.0.{ctx.Api.ApiVersion}"));

        foreach (var service in ctx.Api.Services)
        {
            state.ResetForService();
            await GenerateService(state, service, ctx);
        }

        // The OpenAPI document has no data lake exports, so there is no data lake index and there are no data lake pages
    }

    private static async Task GenerateSidebar(ApiOutputContext<ApiDocsOptions> ctx)
    {
        var entries = ctx.Api.Services
            .Select(service => new ServiceIndex.Entry
            {
                Name = service.Name,
                Method = service.Method
            })
            .OrderBy(x => x.Name).ToImmutableArray();

        var sidebar = new ServiceIndex
        {
            Entries = entries,
            Version = $"2.0.{ctx.Api.ApiVersion}"
        };

        await ctx.Writer.WriteFileAsync("eva/index.json", JsonSerializer.Serialize(sidebar, JsonContext.Default.ServiceIndex));
    }

    private static async Task GenerateService(State state, EvaService service, ApiOutputContext<ApiDocsOptions> ctx)
    {
        var result = new SingleService
        {
            Name = service.Name,
            Path = service.Path,
            Method = service.Method,
            Types = new Dictionary<string, List<TypeInfo>>()
        };

        var requestType = ctx.Api.GetType(service.RequestTypeID);
        var supportsExternalID = SupportsExternalIdsMode(state, ctx.Api, service.RequestTypeID);

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

    private static string? GetDeprecationMessage(EvaService service)
    {
        if (service.Deprecated is { } deprecated)
        {
            return $"\n\n**Deprecated since {deprecated.Announced}:** {deprecated.Comment}\n\n**Will be removed from the typings in {deprecated.Active}**";
        }
        return null;
    }

    private static string GetAuthDescription(EvaService service)
    {
        var permission = service.Auth;
        if (permission is { Functionality: null, Scope: null, UserTypes: null })
        {
            return "This service does not require authentication.";
        }

        return permission switch
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

    private static List<RequestSample> GetRequestSamples(ApiOutputContext<ApiDocsOptions> ctx, string serviceRequestType, string serviceName)
    {
      var sample = BuildMinimalSample(ctx, serviceRequestType, new Stack<string>());
      var sampleStr = sample.ToJsonString(_jsonSerializerOptions);

      return new List<RequestSample>
      {
        new RequestSample
        {
          Name = "JSON",
          Sample = sample.ToJsonString(_jsonSerializerOptions),
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

    private static List<ResponseSample> GetResponseSamples(ApiOutputContext<ApiDocsOptions> ctx, string serviceRequestType, string serviceName)
    {
        var sample = BuildMinimalSample(ctx, serviceRequestType, new Stack<string>());

        // Sample is awesome, but contains some fields that are only filled in specific scenario's
        if (sample is JsonObject jo)
        {
          jo.Remove("Error");
          if (serviceName.EndsWith("_AsyncResult"))
          {
            var metadata = jo["Metadata"] as JsonObject;
            var propsToRemove = metadata!.Select(m => m.Key).Where(k => k != "IsAsyncResultAvailable").ToList();
            foreach (var prop in propsToRemove)
            {
              metadata!.Remove(prop);
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
                Sample = sample.ToJsonString(_jsonSerializerOptions)
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

    private static JsonNode BuildMinimalSample(ApiOutputContext<ApiDocsOptions> ctx, string typeID, Stack<string> recursionGuard)
    {
      var type = ctx.Api.GetType(typeID);

      // Enums
      if (type.IsEnum)
      {
        // We'll just choose the first one (by name, the value without the flags it combines)
        return type.EnumValues
          .OrderBy(v => v.Name, Comparer<string>.Default)
          .Select(v => type.IsFlags ? type.FlagComposition(v).remainder : v.Value)
          .FirstOrDefault(v => v != 0);
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

    private static JsonNode BuildMinimalSample(ApiOutputContext<ApiDocsOptions> ctx, EvaProperty spec, TypeRef type, Stack<string> recursionGuard)
    {
      if (type is PrimitiveRef { Kind: PrimitiveKind.Bool })
      {
        return true;
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Int16 or PrimitiveKind.Int32 or PrimitiveKind.Int64 })
      {
        return 123;
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Float32 or PrimitiveKind.Float64 or PrimitiveKind.Decimal })
      {
        return 123.456f;
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Guid })
      {
        return "DB422695-4164-4046-8E63-83CDAE00D364";
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Date })
      {
        return "2025-01-01";
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Duration })
      {
        return TimeSpan.FromHours(1).ToString();
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Any })
      {
        return "any valid json value";
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.String or PrimitiveKind.ID })
      {
        if (spec.AllowedValues.Any())
        {
          return spec.AllowedValues.First();
        }

        var val = "string";
        if (StringLengthConstraint(spec) is { max: < 6 } slc)
        {
          return val[..slc.max];
        }

        return val;
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Binary })
      {
        return "base64 encoded binary data";
      }

      if (type is MapRef map)
      {
        var result = new JsonObject();
        result["key"] = BuildMinimalSample(ctx, spec, map.Value, recursionGuard);
        return result;
      }

      if (type is OptionRef option)
      {
        return BuildMinimalSample(ctx, spec, option.Options.FirstOrDefault() ?? option.Shared, recursionGuard);
      }

      if (type is ArrayRef array)
      {
        return new JsonArray { BuildMinimalSample(ctx, spec, array.Items, recursionGuard) };
      }

      if (type is PrimitiveRef { Kind: PrimitiveKind.Object })
      {
        var result = new JsonObject();
        result["key"] = "any valid json value";
        return result;
      }

      if (type is NamedRef named)
      {
        return BuildMinimalSample(ctx, named.TypeID, recursionGuard);
      }

      return "unknown";
    }

    /// <summary>
    /// The minimum and maximum length of a string property, when it has either.
    /// </summary>
    private static (int min, int max)? StringLengthConstraint(EvaProperty prop) =>
      prop.MinLength != null || prop.MaxLength != null ? (prop.MinLength ?? 0, prop.MaxLength ?? 0) : null;

    private static string BuildTypeInfo(State state, Dictionary<string, List<TypeInfo>> types, ApiOutputContext<ApiDocsOptions> ctx, string typeID)
    {
        if (state.TypeMapping.TryGetValue(typeID, out var mapped))
        {
            return mapped;
        }

        var id = ToBase26(state.nextID++);
        state.TypeMapping[typeID] = id;

        var type = ctx.Api.GetType(typeID);
        var result = new List<TypeInfo>();

        foreach (var (propname, prop) in type.Properties)
        {
            var ti = new TypeInfo
            {
                Name = propname,
                Required = !prop.Skippable && (prop.Required?.IsEnforced ?? false),
                Type = "unknown",
                Description = prop.Description ?? string.Empty
            };

            (ti.Type, ti.PropertiesID, var options, var extraDescription) = ToType(state, types, ctx, prop.Type);
            if(extraDescription != null) ti.Description += "\n" + extraDescription;
            ti.OneOf = options?.ToArray();
            result.Add(ti);

            if (prop.Entity is { Name: var dmn })
            {
              ti.Description += $"\n\nThis is the ID of a `{dmn}`";
            }

            if (StringLengthConstraint(prop) is { } slc)
            {
              ti.Description += $"\n\nThis string must be between {slc.min} (incl) and {slc.max} (incl) characters long.";
            }

            if (prop.Pattern is { } pattern)
            {
              ti.Description += $"\n\nThis string must be formatted like `{pattern}`.";
            }

            if (prop is { Skippable: true, Required: { IsEnforced: true } })
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

            if (prop.Required is { Pending: { } required })
            {
              ti.Description += $"\n\n**Required since {required.Announced}:** {required.Comment}\n\n**Will be enforced in {required.Active}**";
            }

            if (prop.Deprecated is { } deprecated)
            {
              ti.Deprecation = $"\n\n**Deprecated since {deprecated.Announced}:** {deprecated.Comment}\n\n**Will be removed in {deprecated.Active}**";
              ti.Description += $"\n\n**Deprecated since {deprecated.Announced}:** {deprecated.Comment}\n\n**Will be removed in {deprecated.Active}**";
            }

            ti.Description = ti.Description.Trim();
        }

        types[id] = result;

        return id;
    }

    private static (string type, string? properties, List<OneOf>? oneof, string? description) ToType(State state, Dictionary<string, List<TypeInfo>> types, ApiOutputContext<ApiDocsOptions> ctx, TypeRef type,
        bool? forceNullable = null)
    {
        string? properties = null;
        List<OneOf>? oneof = null;
        string? description = null;

        var result = type is PrimitiveRef primitive
            ? primitive.Kind switch
            {
                PrimitiveKind.Bool => "boolean",
                PrimitiveKind.Int16 or PrimitiveKind.Int32 or PrimitiveKind.Int64 => "integer",
                PrimitiveKind.Float32 or PrimitiveKind.Float64 or PrimitiveKind.Decimal => "float",
                PrimitiveKind.Guid => "guid",
                PrimitiveKind.Date => "datetime",
                PrimitiveKind.String or PrimitiveKind.ID => "string",
                PrimitiveKind.Binary => "binary",
                PrimitiveKind.Any => "any",
                PrimitiveKind.Duration => "duration",
                PrimitiveKind.Object => "object",
                _ => null
            }
            : null;

        if (type is MapRef map)
        {
            var (s1, s2, _, s3) = ToType(state, types, ctx, map.Value, false);

            result = $"map[{s1}]";
            properties = s2;
            description = s3;
        }
        else if (type is OptionRef option)
        {
            var (s1, s2, _, s3) = ToType(state, types, ctx, option.Shared, false);

            result = $"one-of[{s1}]";
            properties = s2;
            description = s3;

            oneof = new List<OneOf>();
            foreach (var x in option.Options)
            {
                var (_, x2, _, _) = ToType(state, types, ctx, x, false);
                oneof.Add(new OneOf { Name = x.TypeID[(x.TypeID.LastIndexOf('.') + 1)..], PropertiesID = x2 });
            }
        }
        else if (type is ArrayRef array)
        {
            var (s1, s2, _, s3) = ToType(state, types, ctx, array.Items, false);

            result = $"array[{s1}]";
            properties = s2;
            description = s3;
        }
        else if (result == null)
        {
          var typeID = ((NamedRef)type).TypeID;
          var targetType = ctx.Api.GetType(typeID);
          if (targetType.IsEnum)
          {

            var totals = targetType.EnumValuesByValue;
            var possibleValues = string.Join('\n', totals.Select(kv => $"* `{kv.Value}` - {kv.Name}"));

            if (targetType.IsFlags)
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
            properties = BuildTypeInfo(state, types, ctx, typeID);
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

    private static (bool backendID, bool systemID) SupportsExternalIdsMode(State state, EvaApi api, string s)
    {
        if (state.SupportsBackendIDCache.TryGetValue(s, out var cached))
        {
            return cached;
        }

        var result = SupportsExternalIdsMode_Uncached(api, s, new HashSet<string>());
        state.SupportsBackendIDCache[s] = result;
        return result;
    }

    private static (bool backendID, bool systemID) SupportsExternalIdsMode_Uncached(EvaApi api, string s, HashSet<string> recursionGuard)
    {
        if (!recursionGuard.Add(s)) return (false, false);

        var type = api.GetType(s);
        var typeDependencies = TypeDependencies(type).ToList();

        var backendID = type.Properties.Any(p =>
            p.Value.Entity is { BackendID: true })
            || typeDependencies.Any(d => SupportsExternalIdsMode_Uncached(api, d, recursionGuard).backendID);

        var systemID = type.Properties.Any(p =>
            p.Value.Entity is { SystemID: true })
            || typeDependencies.Any(d => SupportsExternalIdsMode_Uncached(api, d, recursionGuard).systemID);

        recursionGuard.Remove(s);
        return (backendID, systemID);
    }

    /// <summary>
    /// The types a type refers to (through its base type and properties, generic arguments and options included) and the type it is nested in.
    /// </summary>
    private static IEnumerable<string> TypeDependencies(EvaType type)
    {
        return (type.Extends == null ? [] : type.Extends.Descendants())
            .Concat(type.Properties.Values.SelectMany(p => p.Type.Descendants()))
            .OfType<NamedRef>()
            .Select(n => n.TypeID)
            .Concat(type.Parent == null ? [] : [type.Parent.ID])
            .Distinct();
    }

    private static string MapUserTypes(EvaUserTypes t)
    {
        var values = Enum.GetValues<EvaUserTypes>()
            .Where(v => v != EvaUserTypes.None && t.HasFlag(v))
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
