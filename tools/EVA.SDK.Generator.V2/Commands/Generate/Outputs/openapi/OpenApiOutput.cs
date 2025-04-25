using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Transforms.Internal;
using EVA.SDK.Generator.V2.Exceptions;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Validations;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;

internal partial class OpenApiOutput : IOutput<OpenApiOptions>
{
  private class State
  {
    internal readonly Dictionary<string, (bool backendID, bool systemID)> SupportsBackendIDCache = new();
  }

  public string? OutputPattern => null;

  public bool GetForcedTransformations(OpenApiOptions _, INamedTransform x) =>
    x is RemoveGenerics or RemoveUnusedGenericArguments or RemoveErrors or RemoveEventExports or RemoveInheritance;

  private const string Parameter_Header_UserAgent = "p1";
  private const string Parameter_Header_IdsMode = "p2";
  private const string Parameter_Header_IdsModeSystem = "p6";
  private const string Parameter_Header_AsyncCallback = "p3";
  private const string Parameter_Header_OrganizationUnit = "p4";
  private const string Parameter_Header_OrganizationUnitQuery = "p5";

  private const string Schema_Error = "eva_error_400";
  private const string Example_400_RequestValidation = "eva_example_400_RequestValidationFailure";
  private const string Example_403_Forbidden = "eva_example_403_Forbidden";

  public async Task Write(OutputContext<OpenApiOptions> ctx)
  {
    var additionalExamples = await LoadAdditionalExamples(ctx);
    var model = GetModel(ctx.Input, ctx.Options.Host, ctx.Options.Api, additionalExamples);

    foreach (var error in model.Validate(ValidationRuleSet.GetDefaultRuleSet()))
    {
      ctx.Logger.LogWarning("Validation error: {Error}", error.ToString());
    }

    var version = ctx.Options.Version switch
    {
      "v2" => OpenApiSpecVersion.OpenApi2_0,
      _ => OpenApiSpecVersion.OpenApi3_0
    };

    var filename = ctx.Options.Format == "yaml" ? "openapi.yaml" : "openapi.json";
    await using var file = ctx.Writer.WriteStreamAsync(filename);
    await using var textWriter = new StreamWriter(file.Value);

    IOpenApiWriter openApiWriter = ctx.Options.Format == "yaml"
      ? new OpenApiYamlWriter(textWriter, new OpenApiWriterSettings())
      : new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings { Terse = ctx.Options.Terse });

    model.Serialize(openApiWriter, version);
  }

  internal static string Cleanup(ApiDefinitionModel input)
  {
    string? result = null;
    // Remove the Error field from all response messages
    foreach (var service in input.Services)
    {
      var responseType = input.Types[service.ResponseTypeID];
      if (responseType.Properties.TryGetValue("Error", out var v))
      {
        result = v.Type.Name;
        responseType.Properties = responseType.Properties.Remove("Error");
      }
    }

    return result;
  }

  internal OpenApiDocument GetModel(
    ApiDefinitionModel input,
    string host,
    string api,
    Dictionary<string, List<(int statusCode, string name, string content)>> additionalExamples)
  {
    var state = new State();
    var errorObjectID = Cleanup(input);

    var iseva = api == "eva";

    var serverVariables = new Dictionary<string, OpenApiServerVariable>
    {
      { "customer", new OpenApiServerVariable { Default = "acme" } }
    };

    var servers = !string.IsNullOrWhiteSpace(host)
      ? [new OpenApiServer { Url = host }]
      : !iseva
        ? [new OpenApiServer { Url = "https://my.api.com" }]
        : new List<OpenApiServer>
        {
          new()
          {
            Description = "Testing environment",
            Url = "https://api.euw.{customer}.test.eva-online.cloud",
            Variables = serverVariables
          },
          new()
          {
            Description = "Acceptance environment",
            Url = "https://api.euw.{customer}.acc.eva-online.cloud",
            Variables = serverVariables
          },
          new()
          {
            Description = "Production environment",
            Url = "https://api.euw.{customer}.prod.eva-online.cloud",
            Variables = serverVariables
          }
        };

    var tags = input.Services
        .Select(s => TagFromAssembly(s.Assembly))
        .Concat(["DataLake", "API"])
        .Distinct()
        .Order()
        .Select(s => new OpenApiTag { Name = s, Description = s })
        .ToList();

    // Base
    List<OpenApiSecurityRequirement> securityRequirements =
    [
      new OpenApiSecurityRequirement
      {
        {
          new OpenApiSecurityScheme
          {
            Reference = new OpenApiReference
            {
              Type = ReferenceType.SecurityScheme,
              Id = iseva ? "eva-auth" : "api-auth"
            }
          },
          new List<string>()
        }
      }
    ];

    var model = new OpenApiDocument
    {
      Info = new OpenApiInfo
      {
        Version = "1.0.0",
        Description = "OpenApi description of EVA",
        Title = "EVA",
        Contact = new OpenApiContact
        {
          Name = "New Black",
          Url = new Uri("https://support.newblack.io/")
        }
      },
      Servers = servers,
      Paths = new OpenApiPaths(),
      Components = new OpenApiComponents(),
      Tags = tags,
      SecurityRequirements = securityRequirements
    };

    model.Components.SecuritySchemes.Add("eva-auth", new OpenApiSecurityScheme
    {
      Description = "The default authentication mechanism when communicating with EVA",
      Type = SecuritySchemeType.ApiKey,
      Name = "Authorization",
      In = ParameterLocation.Header
    });

    if (iseva)
    {
      model.Components.SecuritySchemes.Add("eva-auth-elevated", new OpenApiSecurityScheme
      {
        Name = "EVA-Elevation-Token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Authenticate using an elevated token. This allows temporary access to resources that are otherwise not accessible."
      });
      model.Components.SecuritySchemes.Add("eva-auth-apptoken", new OpenApiSecurityScheme
      {
        Name = "EVA-App-Token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Authenticate using an application token. This allows building an order as a non-logged in user."
      });
    }
    else
    {
      model.Components.SecuritySchemes.Add("api-auth", new OpenApiSecurityScheme
      {
        Description = "API key for external service",
        Type = SecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = ParameterLocation.Header
      });
    }

    // Parameters
    model.Components.Parameters.Add(Parameter_Header_UserAgent, new()
    {
      In = ParameterLocation.Header,
      Name = "EVA-User-Agent",
      Description = "The user agent that is making these calls. Don't make this specific per device/browser but per application. This should be of the form: `MyFirstUserAgent/1.0.0`",
      Required = true,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Default = new OpenApiString("eva-sdk-openapi"),
        Type = "string"
      },
      Style = ParameterStyle.Simple
    });
    model.Components.Parameters.Add(Parameter_Header_IdsMode, new()
    {
      In = ParameterLocation.Header,
      Description = "The IDs mode to run this request in. Currently only `ExternalIDs` is supported.",
      Name = "EVA-IDs-Mode",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "string",
        Enum = new List<IOpenApiAny>
        {
          new OpenApiString("ExternalIDs")
        }
      },
      Style = ParameterStyle.Simple
    });
    model.Components.Parameters.Add(Parameter_Header_IdsModeSystem, new()
    {
      In = ParameterLocation.Header,
      Description = "The ID of the backend system that is used to resolve the IDs.",
      Name = "EVA-IDs-BackendSystemID",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "string"
      },
      Style = ParameterStyle.Simple
    });

    model.Components.Parameters.Add(Parameter_Header_OrganizationUnit, new()
    {
      In = ParameterLocation.Header,
      Description = "The ID of the organization unit to run this request in.",
      Name = "EVA-Requested-OrganizationUnitID",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "integer"
      },
      Style = ParameterStyle.Simple
    });

    model.Components.Parameters.Add(Parameter_Header_OrganizationUnitQuery, new()
    {
      In = ParameterLocation.Header,
      Description = "The query that selects the organization unit to run this request in.",
      Name = "EVA-Requested-OrganizationUnit-Query",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "string"
      },
      Style = ParameterStyle.Simple
    });

    model.Components.Parameters.Add(Parameter_Header_AsyncCallback, new()
    {
      In = ParameterLocation.Header,
      Name = "EVA-Async-Callback",
      Description =
        "Indicate how the caller should be notified when the asynchronous operation is complete. This is a serialized JSON object. Currently we only support the `email` property. Use `me` as a value to be notified on the emailaddress of the current user.",
      Required = false,
      AllowEmptyValue = false,
      Schema = new OpenApiSchema
      {
        Type = "string",
        Default = new OpenApiString("{}")
      },
      Style = ParameterStyle.Simple
    });

    // Render each datalake endpoint
    foreach (var dl in input.DatalakeExports)
    {
      model.Paths["/datalake/" + dl.Name] = ToDatalakeItem(input, dl);
    }

    // Render each service
    foreach (var service in input.Services)
    {
      var pathItem = ToPathItem(state, input, service, additionalExamples, iseva || service.Api.EndsWith("-callbacks"));
      model.Paths[service.Path] = pathItem;
    }

    // Render each type
    foreach (var (id, type) in input.Types)
    {
      model.Components.Schemas.Add(FixName(id), ToSchema(input, type));
    }

    // 400 error
    model.Components.Schemas.Add(Schema_Error, new OpenApiSchema
    {
      Type = "object",
      Required = new HashSet<string> { "Error" },
      Properties = new Dictionary<string, OpenApiSchema> { { "Error", ToSchema(errorObjectID) } }
    });

    model.Components.Examples.Add(Example_400_RequestValidation, new OpenApiExample
    {
      Summary = "An example BadRequest response",
      Value = new OpenApiObject
      {
        ["Error"] = new OpenApiObject
        {
          ["Message"] = new OpenApiString("Validation of the request message failed: Field ABC has an invalid value for a Product"),
          ["Type"] = new OpenApiString("RequestValidationFailure"),
          ["Code"] = new OpenApiString("COVFEFE"),
          ["RequestID"] = new OpenApiString("576b62dd71894e3281a4d84951f44e70")
        }
      }
    });

    model.Components.Examples.Add(Example_403_Forbidden, new OpenApiExample
    {
      Summary = "An example Forbidden response",
      Value = new OpenApiObject
      {
        ["Error"] = new OpenApiObject
        {
          ["Message"] = new OpenApiString("You are not authorized to execute this request."),
          ["Type"] = new OpenApiString("Forbidden"),
          ["Code"] = new OpenApiString("COVFEFE"),
          ["RequestID"] = new OpenApiString("576b62dd71894e3281a4d84951f44e70")
        }
      }
    });

    return model;
  }

  private static OpenApiSchema ToSchema(ApiDefinitionModel input, TypeSpecification type)
  {
    if (type.EnumIsFlag.HasValue)
    {
      var totals = type.EnumValues.ToTotals();
      var possibleValues = string.Join('\n', totals.OrderBy(kv => kv.Value).Select(kv => $"* `{kv.Value}` - {kv.Key}"));

      if (type.EnumIsFlag is true)
      {
        return new OpenApiSchema
        {
          Type = "integer",
          Description = $"Flags enum, combine any of the below values:\n\n{possibleValues}"
        };
      }

      if (type.EnumIsFlag is false)
      {
        return new OpenApiSchema
        {
          Type = "integer",
          Enum = totals.Select(kv => new OpenApiInteger((int)kv.Value) as IOpenApiAny).ToList(),
          Description = $"Possible values:\n\n{possibleValues}"
        };
      }
    }

    var result = new OpenApiSchema
    {
      Type = "object",
      AdditionalPropertiesAllowed = false,
      Properties = new Dictionary<string, OpenApiSchema>()
    };

    foreach (var (name, prop) in type.Properties)
    {
      string? dmBackendId = null;
      if (prop.DataModelInformation is { SupportsBackendID: true } dmi)
      {
        dmBackendId = dmi.Name;
      }

      var schema = ToSchema(input, prop.Type, dmBackendId);
      schema.Description = prop.Description ?? string.Empty;

      if (prop.DataModelInformation is { Name: var dmn })
      {
        schema.Description += $"\n\nThis is the ID of a `{dmn}`";
      }

      if (prop.StringLengthConstraint is { } slc)
      {
        schema.MinLength = slc.Min;
        schema.MaxLength = slc.Max;
        schema.Description += $"\n\nThis string must be between {slc.Min} (incl) and {slc.Max} (incl) characters long.";
      }

      if (prop.StringRegexConstraint is { } src)
      {
        schema.Pattern = src.Regex;
        schema.Description += $"\n\nThis string must be formatted like `{src.Regex}`.";
      }

      if (prop is { Skippable: true, Required: { CurrentValue: true } })
      {
        schema.Description += $"\n\nWhile this property is not required, if it is sent in the request it must have a valid value.";
      }
      else if (prop is { Skippable: true, Type.Nullable: true })
      {
        schema.Description += $"\n\nProviding a `null` value and not providing the property at all has different meanings.";
      }

      if (prop.AllowedValues is { Length: > 0 } allowedValues)
      {
        schema.Enum = allowedValues.Select<string, IOpenApiAny>(v => new OpenApiString(v)).ToList();
      }

      if (prop.Required is { Effective: not null } required)
      {
        schema.Description += $"\n\n**Required since {required.Introduced}:** {required.Comment}\n\n**Will be enforced in {required.Effective}**";
      }

      if (prop.Deprecated is { } deprecated)
      {
        schema.Deprecated = true;
        schema.Description += $"\n\n**Deprecated since {deprecated.Introduced}:** {deprecated.Comment}\n\n**Will be removed in {deprecated.Effective}**";
      }

      schema.Description = schema.Description.Trim();

      result.Properties.Add(name, schema);
    }

    result.Required = type.Properties
      .Where(p => p.Value is { Type.Nullable: false, Skippable: false })
      .Select(p => p.Key)
      .OrderBy(x => x)
      .ToImmutableHashSet();

    if (type.Extends != null)
    {
      result.AllOf = new List<OpenApiSchema> { ToSchema(input, type.Extends) };
    }

    return result;
  }

  private static OpenApiSchema ToSchema(ApiDefinitionModel input, TypeReference type, string? dmBackendID = null)
  {
    var s = ToSchema_IgnoreNull(input, type, dmBackendID);
    if (type.Nullable) s.Nullable = true;
    return s;
  }

  private static OpenApiSchema ToSchema_IgnoreNull(ApiDefinitionModel input, TypeReference type, string? dmBackendID)
  {
    if (type.Name is ApiSpecConsts.Int16 or ApiSpecConsts.Int32 or ApiSpecConsts.Int64 && dmBackendID != null)
    {
      var s = ToSchema_IgnoreNull(input, type, null);
      s.Title = $"{dmBackendID} ID";
      return new OpenApiSchema
      {
        OneOf = new List<OpenApiSchema>
        {
          s,
          new() { Type = "string", Title = $"{dmBackendID} BackendID", Description = "Make sure to set the `EVA-IDs-Mode` header to `ExternalIDs` when using this" },
        }
      };
    }

    if (type.Name == ApiSpecConsts.Int16) return new OpenApiSchema { Type = "integer" };
    if (type.Name == ApiSpecConsts.Int32) return new OpenApiSchema { Type = "integer", Format = "int32" };
    if (type.Name == ApiSpecConsts.Int64) return new OpenApiSchema { Type = "integer", Format = "int64" };
    if (type.Name == ApiSpecConsts.ID) return new OpenApiSchema { Type = "integer", Format = "int64" };
    if (type.Name == ApiSpecConsts.String) return new OpenApiSchema { Type = "string" };
    if (type.Name == ApiSpecConsts.Binary) return new OpenApiSchema { Type = "string", Format = "byte" };
    if (type.Name == ApiSpecConsts.Bool) return new OpenApiSchema { Type = "boolean" };
    if (type.Name == ApiSpecConsts.Guid) return new OpenApiSchema { Type = "string", Format = "uuid" };
    if (type.Name == ApiSpecConsts.Float32) return new OpenApiSchema { Type = "number", Format = "float" };
    if (type.Name == ApiSpecConsts.Float64) return new OpenApiSchema { Type = "number", Format = "double" };
    if (type.Name == ApiSpecConsts.Float128) return new OpenApiSchema { Type = "number", Format = "double" };
    if (type.Name == ApiSpecConsts.Duration) return new OpenApiSchema { Type = "string", Format = "duration" };
    if (type.Name == ApiSpecConsts.Object) return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == ApiSpecConsts.Any) return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == ApiSpecConsts.Date) return new OpenApiSchema { Type = "string", Format = "date-time" };
    if (type.Name == ApiSpecConsts.Specials.Array) return new OpenApiSchema { Type = "array", Items = ToSchema(input, type.Arguments.Single(), dmBackendID) };

    if (type.Name == ApiSpecConsts.Specials.Option)
    {
      return new OpenApiSchema
      {
        AnyOf = type.Arguments
          .Select(a => ToSchema(input, a))
          .ToList()
      };
    }

    if (type.Name == ApiSpecConsts.Specials.Map)
    {
      // We can't really make a map with certain key types work, but this is the best we can do regardless of the key type
      return new OpenApiSchema
      {
        Type = "object",
        AdditionalPropertiesAllowed = true,
        AdditionalProperties = ToSchema(input, type.Arguments[1])
      };
    }

    if (!type.Name.StartsWith("_") && !type.Arguments.Any())
    {
      return ToSchema(type.Name);
    }

    throw new SdkException($"Cannot build openapi schema from {type.Name}");
  }

  private static OpenApiSchema ToSchema(string id)
  {
    return new OpenApiSchema
    {
      Reference = new OpenApiReference
      {
        Id = FixName(id),
        Type = ReferenceType.Schema
      }
    };
  }

  private static OpenApiPathItem ToDatalakeItem(ApiDefinitionModel input, DatalakeExportTarget dl)
  {
    var type = input.Types[dl.DataType];

    var description = $"Not a real service, but the response shows the format of the {dl.Name} export.";
    if (dl.ExamplePath != null) description += $" This export gets written to the following path: `{dl.ExamplePath}`";

    return new OpenApiPathItem
    {
      Summary = dl.Name,
      Description = description,
      Parameters = new List<OpenApiParameter>(),
      Operations = new Dictionary<OperationType, OpenApiOperation>
      {
        [OperationType.Get] = new()
        {
          Summary = $"{dl.Name} Export",
          Description = description,
          OperationId = $"datalake_{dl.Name}",
          Tags = new List<OpenApiTag> { new() { Name = TagFromAssembly(type.Assembly), Description = TagFromAssembly(type.Assembly) } },
          Security = new List<OpenApiSecurityRequirement>(),
          Responses = new OpenApiResponses
          {
            ["200"] = new()
            {
              Description = $"The format of the {dl.Name} export.",
              Content = new Dictionary<string, OpenApiMediaType>
              {
                ["application/json"] = new() { Schema = ToSchema(dl.DataType) }
              }
            }
          }
        }
      }
    };
  }

  private static OpenApiPathItem ToPathItem(
    State state,
    ApiDefinitionModel input,
    ServiceModel service,
    Dictionary<string, List<(int statusCode, string name, string content)>> additionalExamples,
    bool iseva)
  {
    string MapUserTypes(ApiSpecUserTypes t)
    {
      var values = Enum.GetValues<ApiSpecUserTypes>();
      values = values
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

    var description = input.Types[service.RequestTypeID].Description ?? $"The {service.Name} service";

    var requiresAuthentication = !service.AuthInformation.RequiredPermissions.All(p => p is { Functionality: null, Scope: null, UserTypes: null });
    var supportsExternalID = SupportsExternalIdsMode(state, input, service.RequestTypeID);

    if (requiresAuthentication)
    {
      description += "\n\n---";

      foreach (var x in service.AuthInformation.RequiredPermissions)
      {
        description += "\n**Authentication:**\n\n";
        description += x switch
        {
          { Functionality: null, Scope: null, UserTypes: > 0 } => $"Requires a user of type {MapUserTypes(x.UserTypes.Value)}",
          { Functionality: not null, Scope: null, UserTypes: > 0 } => $"Requires a user of type {MapUserTypes(x.UserTypes.Value)} with functionality `{x.Functionality}`",
          { Functionality: not null, Scope: not null, UserTypes: > 0 } => $"Requires a user of type {MapUserTypes(x.UserTypes.Value)} with functionality `{x.Functionality}:{x.Scope}`",
          { Functionality: not null, Scope: null } => $"Requires any user with functionality `{x.Functionality}`",
          { Functionality: not null, Scope: not null } => $"Requires any user with functionality `{x.Functionality}:{x.Scope}`",
          { Functionality: null, Scope: null } => "This service does not require authentication",
          _ => "> ???"
        };
      }
    }

    if (service.Deprecated is { } deprecated)
    {
      description += "\n\n---";
      description += "\n**Deprecated:**\n\n";
      description += $"\n\n**Deprecated since {deprecated.Introduced}:** {deprecated.Comment}\n\n**Will be removed from the typings in {deprecated.Effective}**";
    }

    List<OpenApiParameter> parameters = !iseva
      ? []
      :
      [
        new()
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.Parameter,
            Id = Parameter_Header_UserAgent
          }
        },

        new()
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.Parameter,
            Id = Parameter_Header_OrganizationUnit
          }
        },

        new()
        {
          Reference = new OpenApiReference
          {
            Type = ReferenceType.Parameter,
            Id = Parameter_Header_OrganizationUnitQuery
          }
        }
      ];

    if (supportsExternalID.backendID)
    {
      parameters.Add(new()
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.Parameter,
          Id = Parameter_Header_IdsMode
        }
      });
    }

    if (supportsExternalID.systemID)
    {
      parameters.Add(new()
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.Parameter,
          Id = Parameter_Header_IdsModeSystem
        }
      });
    }

    if (service.Name.EndsWith("_Async"))
    {
      parameters.Add(new()
      {
        Reference = new OpenApiReference
        {
          Type = ReferenceType.Parameter,
          Id = Parameter_Header_AsyncCallback
        }
      });
    }

    var openApiTags = iseva
      ? new List<OpenApiTag> { new() { Name = TagFromAssembly(service.Assembly), Description = TagFromAssembly(service.Assembly) } }
      : new List<OpenApiTag> { new() { Name = "API" } };

    var result = new OpenApiPathItem
    {
      Summary = service.Name,
      Description = description,
      Operations = new Dictionary<OperationType, OpenApiOperation>
      {
        {
          OperationType.Post, new OpenApiOperation
          {
            Summary = service.Name,
            Description = description,
            OperationId = service.Name,
            Deprecated = service.Deprecated is not null,
            Tags = openApiTags,
            Parameters = parameters,
            Security = !requiresAuthentication
              ? new List<OpenApiSecurityRequirement>()
              : new List<OpenApiSecurityRequirement>
              {
                new()
                {
                  {
                    new OpenApiSecurityScheme
                    {
                      Reference = new OpenApiReference
                      {
                        Type = ReferenceType.SecurityScheme,
                        Id = iseva ? "eva-auth" : "api-auth"
                      }
                    },
                    Array.Empty<string>()
                  }
                }
              },
            RequestBody = new OpenApiRequestBody
            {
              Description = "",
              Required = true,
              Content = new Dictionary<string, OpenApiMediaType>
              {
                ["application/json"] = new()
                {
                  Schema = ToSchema(service.RequestTypeID),
                  Examples = iseva && additionalExamples.TryGetValue(service.Name, out var examples)
                    ? examples.ToDictionary(x => x.name, x => new OpenApiExample
                    {
                      Value = new OpenApiStringObject(x.content)
                    })
                    : []
                }
              }
            },
            Responses = new OpenApiResponses
            {
              ["200"] = new()
              {
                Description = $"The response for a call to {service.Name}",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                  ["application/json"] = new()
                  {
                    Schema = ToSchema(service.ResponseTypeID)
                  }
                }
              }
            }
          }
        }
      }
    };

    if (iseva)
    {
      var responses = result.Operations.First().Value.Responses;
      responses["400"] = new()
      {
        Description = "A BadRequest response",
        Content = new Dictionary<string, OpenApiMediaType>
        {
          ["application/json"] = new()
          {
            Schema = new OpenApiSchema { Reference = new OpenApiReference { Id = Schema_Error, Type = ReferenceType.Schema } },
            Examples = new Dictionary<string, OpenApiExample>
            {
              ["RequestValidationFailure"] = new()
              {
                Reference = new OpenApiReference { Type = ReferenceType.Example, Id = Example_400_RequestValidation }
              }
            }
          }
        }
      };

      responses["403"] = new()
      {
        Description = "A Forbidden response",
        Content = new Dictionary<string, OpenApiMediaType>
        {
          ["application/json"] = new()
          {
            Schema = new OpenApiSchema { Reference = new OpenApiReference { Id = Schema_Error, Type = ReferenceType.Schema } },
            Examples = new Dictionary<string, OpenApiExample>
            {
              ["Forbidden"] = new()
              {
                Reference = new OpenApiReference { Type = ReferenceType.Example, Id = Example_403_Forbidden }
              }
            }
          }
        }
      };
    }

    return result;
  }

  private static async Task<Dictionary<string, List<(int statusCode, string name, string content)>>> LoadAdditionalExamples(OutputContext<OpenApiOptions> ctx)
  {
    var result = new Dictionary<string, List<(int statusCode, string name, string content)>>(StringComparer.OrdinalIgnoreCase);

    var dir = ctx.Options.ExamplesFolder;
    if (dir == null) return result;

    if (!Directory.Exists(dir))
    {
      ctx.Logger.LogError("Examples directory {Directory} does not exist", dir);
      return result;
    }

    foreach (var exampleFile in Directory.GetFiles(dir))
    {
      // We have to find the service name, the description and the status code (optional)
      // these are all delimited with an underscore
      var fileName = Path.GetFileNameWithoutExtension(exampleFile);
      var parts = fileName.Split('_');

      if (parts.Length is < 2 or > 3)
      {
        ctx.Logger.LogError("Invalid example file name {FileName}", fileName);
        continue;
      }

      var serviceName = parts[0];
      var description = parts[1];
      var statusCode = 200;

      if (parts.Length == 3)
      {
        if (!int.TryParse(parts[2], out statusCode))
        {
          ctx.Logger.LogError("Invalid status code in example file name {FileName}", fileName);
          continue;
        }
      }

      ctx.Logger.LogInformation("Loading example file {FileName}", exampleFile);

      var content = await File.ReadAllTextAsync(exampleFile);

      if (!result.TryGetValue(serviceName, out var list))
      {
        list = new List<(int statusCode, string name, string content)>();
        result.Add(serviceName, list);
      }

      // List could already contain a duplicate
      if (list.Any(x => x.statusCode == statusCode && x.name == description))
      {
        ctx.Logger.LogWarning("Duplicate example file {FileName}", fileName);
        var i = 1;
        while (true)
        {
          var d = $"{description} ({i++})";
          if (list.Any(x => x.statusCode == statusCode && x.name == d)) continue;

          list.Add((statusCode, d, content));
          break;
        }
      }
      else
      {
        list.Add((statusCode, description, content));
      }
    }

    return result;
  }

  private static (bool backendID, bool systemID) SupportsExternalIdsMode(State state, ApiDefinitionModel input, string s)
  {
    if (state.SupportsBackendIDCache.TryGetValue(s, out var cached)) return cached;

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

  private static string FixName(string name)
  {
    return NameRegex().Replace(name, "_").Trim('_');
  }

  private static string TagFromAssembly(string name)
  {
    return name.StartsWith("EVA.") ? name[4..] : name;
  }

  [GeneratedRegex("[^a-zA-Z0-9-._]", RegexOptions.Compiled)]
  private static partial Regex NameRegex();

  /// <summary>
  /// This is a hack, should be improved.
  /// </summary>
  private class OpenApiStringObject : IOpenApiPrimitive
  {
    private readonly string _value;
    public AnyType AnyType => AnyType.Primitive;
    public PrimitiveType PrimitiveType => PrimitiveType.Binary;

    public OpenApiStringObject(string value)
    {
      _value = value;
    }

    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
      writer.WriteRaw(_value);
    }
  }
}