using System.Collections.Immutable;
using System.Text.RegularExpressions;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Exceptions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.openapi;

public class OpenApiOutput : IOutput<OpenApiOptions>
{
  public string? OutputPattern => null;

  public string[] ForcedRemoves => new[] { "generics", "unused-type-params", "errors", "event-exports" };

  public async Task Write(ApiDefinitionModel input, OpenApiOptions options, OutputWriter outputWriter)
  {
    var outputPath = Path.GetFullPath(Path.Combine(options.OutputDirectory, "openapi.json"));
    Console.WriteLine($"Writing OpenAPI file: {outputPath}");

    var model = GetModel(input, options.Host);

    var version = options.Version == "v2" ? OpenApiSpecVersion.OpenApi2_0 : OpenApiSpecVersion.OpenApi3_0;

    await using (var file = outputWriter.WriteStreamAsync("openapi.json"))
    {
      await using var textWriter = new StreamWriter(file.Value);
      IOpenApiWriter writer = options.Format == "yaml"
        ? new OpenApiYamlWriter(textWriter, new OpenApiWriterSettings())
        : new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings { Terse = options.Terse });

      model.Serialize(writer, version);
    }
  }

  internal static OpenApiDocument GetModel(ApiDefinitionModel input, string host)
  {
    // Base
    var model = new OpenApiDocument
    {
      Info = new OpenApiInfo
      {
        Version = "1.0.0",
        Description = "OpenApi description of EVA",
        Title = "EVA",
        Contact = new OpenApiContact
        {
          Email = "ruben.oost@newblack.io",
          Name = "Ruben Oost",
          Url = new Uri("https://newblack.io/")
        }
      },
      Servers = new List<OpenApiServer>
      {
        new OpenApiServer { Url = host }
      },
      Paths = new OpenApiPaths(),
      Components = new OpenApiComponents(),
      SecurityRequirements = new List<OpenApiSecurityRequirement>
      {
        new OpenApiSecurityRequirement
        {
          { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "eva-auth" } }, new List<string>() }
        }
      }
    };

    model.Components.SecuritySchemes.Add("eva-auth", new OpenApiSecurityScheme
    {
      Description = "The default authentication mechanism when communicating with EVA",
      Type = SecuritySchemeType.ApiKey,
      Name = "Authorization",
      In = ParameterLocation.Header
    });

    // Render each service
    foreach (var service in input.Services)
    {
      model.Paths[service.Path] = ToPathItem(input, service);
    }

    // Render each type
    foreach (var (id, type) in input.Types)
    {
      model.Components.Schemas.Add(FixName(id), ToSchema(input, type));
    }

    return model;
  }

  private static OpenApiSchema ToSchema(ApiDefinitionModel input, TypeSpecification type)
  {
    var result = new OpenApiSchema
    {
      Type = "object",
      AdditionalPropertiesAllowed = false,
      Properties = new Dictionary<string, OpenApiSchema>()
    };

    foreach (var (name, prop) in type.Properties)
    {
      var schema = ToSchema(input, prop.Type);
      schema.Description = prop.Description;
      result.Properties.Add(name, schema);
    }

    result.Required = type.Properties.Where(p => !p.Value.Type.Nullable).Select(p => p.Key).OrderBy(x => x).ToImmutableHashSet();

    if (type.Extends != null)
    {
      result.AllOf = new List<OpenApiSchema> { ToSchema(input, type.Extends) };
    }

    return result;
  }

  private static OpenApiSchema ToSchema(ApiDefinitionModel input, TypeReference type)
  {
    if (type.Name == "int16") return new OpenApiSchema { Type = "integer" };
    if (type.Name == "int32") return new OpenApiSchema { Type = "integer" };
    if (type.Name == "int64") return new OpenApiSchema { Type = "integer" };
    if (type.Name == "string") return new OpenApiSchema { Type = "string" };
    if (type.Name == "binary") return new OpenApiSchema { Type = "string" };
    if (type.Name == "bool") return new OpenApiSchema { Type = "boolean" };
    if (type.Name == "guid") return new OpenApiSchema { Type = "string", Format = "uuid" };
    if (type.Name == "float32") return new OpenApiSchema { Type = "number" };
    if (type.Name == "float64") return new OpenApiSchema { Type = "number" };
    if (type.Name == "float128") return new OpenApiSchema { Type = "number" };
    if (type.Name == "duration") return new OpenApiSchema { Type = "string" };
    if (type.Name == "object") return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == "any") return new OpenApiSchema { Type = "object", AdditionalPropertiesAllowed = true };
    if (type.Name == "date") return new OpenApiSchema { Type = "string", Format = "date-time" };
    if (type.Name == "array") return new OpenApiSchema { Type = "array", Items = ToSchema(input, type.Arguments.Single()) };
    if (type.Name == "option") return new OpenApiSchema { AnyOf = type.Arguments.Select(a => ToSchema(input, a)).ToList() };

    if (type.Name == "map")
    {
      var keyType = type.Arguments[0].Name;
      if (keyType is "string" or "int64" or "float128" or "date" || (char.IsUpper(keyType[0]) && input.Types[keyType].EnumIsFlag.HasValue))
      {
        return new OpenApiSchema
        {
          Type = "object",
          AdditionalPropertiesAllowed = true,
          AdditionalProperties = ToSchema(input, type.Arguments[1])
        };
      }
    }

    if (!type.Name.StartsWith("_") && !type.Arguments.Any())
    {
      return ToSchema(type.Name);
    }

    throw new SdkException($"Cannot build openapi schema from {type.Name}");
  }

  private static OpenApiPathItem ToPathItem(ApiDefinitionModel input, ServiceModel service)
  {
    return new OpenApiPathItem
    {
      Summary = service.Name,
      Description = input.Types[service.RequestTypeID].Description ?? $"The {service.Name} service",
      Parameters = new List<OpenApiParameter>
      {
        new OpenApiParameter
        {
          In = ParameterLocation.Header,
          Name = "EVA-User-Agent",
          Required = true,
          AllowEmptyValue = false,
          Schema = new OpenApiSchema
          {
            Default = new OpenApiString("eva-sdk-openapi"),
            Type = "string"
          },
          Style = ParameterStyle.Simple
        }
      },
      Operations = new Dictionary<OperationType, OpenApiOperation>
      {
        {
          OperationType.Post, new OpenApiOperation
          {
            Summary = service.RequestTypeID,
            Description = input.Types[service.RequestTypeID].Description ?? $"The {service.Name} service",
            OperationId = service.Name,
            Tags = new List<OpenApiTag> { new OpenApiTag { Name = TagFromAssembly(service.Assembly), Description = TagFromAssembly(service.Assembly) } },
            Security = new List<OpenApiSecurityRequirement>
            {
              new OpenApiSecurityRequirement
              {
                {
                  new OpenApiSecurityScheme
                  {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "eva-auth" }
                  },
                  ImmutableList<string>.Empty
                }
              }
            },
            RequestBody = new OpenApiRequestBody
            {
              Description = "",
              Required = true,
              Content = new Dictionary<string, OpenApiMediaType>
              {
                {
                  "application/json", new OpenApiMediaType
                  {
                    Schema = ToSchema(service.RequestTypeID)
                  }
                }
              }
            },
            Responses = new OpenApiResponses
            {
              {
                "200", new OpenApiResponse
                {
                  Description = $"The response for a call to {service.Name}",
                  Content = new Dictionary<string, OpenApiMediaType>
                  {
                    {
                      "application/json", new OpenApiMediaType
                      {
                        Schema = ToSchema(service.ResponseTypeID)
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    };
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

  private static readonly Regex _nameRegex = new Regex("[^a-zA-Z0-9-._]", RegexOptions.Compiled);

  private static string FixName(string name)
  {
    return _nameRegex.Replace(name, "_").Trim('_');
  }

  private static string TagFromAssembly(string name)
  {
    return name.StartsWith("EVA.") ? name[4..] : name;
  }
}