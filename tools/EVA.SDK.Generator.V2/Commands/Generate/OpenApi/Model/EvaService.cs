using Microsoft.OpenApi;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

[Flags]
internal enum EvaUserTypes
{
  None = 0,
  Employee = 1,
  Customer = 2,
  Anonymous = 4,
  Business = 8,
  System = 16 | Employee,
  Debtor = 64,
  Tester = 512,
  RemovedByRequest = 1024,
  Api = 2048
}

[Flags]
internal enum EvaFunctionalityScope
{
  None = 0,
  Create = 1,
  Edit = 2,
  Delete = 4,
  View = 8,
  Manage = Create | Edit | Delete | View | 16,
  Settings = 32,
  Scripting = 64,
  All = Manage | Settings | Scripting
}

/// <summary>
/// What a user needs to call a service.
/// </summary>
internal sealed record EvaAuth(EvaUserTypes? UserTypes, string? Functionality, EvaFunctionalityScope? Scope, bool AllowPublic)
{
  internal bool RequiresAuthentication => UserTypes != null || Functionality != null || Scope != null;
}

/// <summary>
/// A service of the API, read from its operation.
/// </summary>
internal sealed class EvaService
{
  private readonly EvaApi _api;

  internal EvaService(EvaApi api, string path, OpenApiOperation operation)
  {
    _api = api;
    Path = path;
    Operation = operation;
  }

  internal string Path { get; }
  internal OpenApiOperation Operation { get; }

  internal string Name => Operation.OperationId!;

  internal string Method => "POST";

  internal string? Description => Operation.Description;

  internal EvaType RequestType => _api.TypeOfComponent(SchemaOf(Operation.RequestBody!.Content!).ComponentName());

  internal EvaType ResponseType => _api.TypeOfComponent(SchemaOf(Operation.Responses!["200"].Content!).ComponentName());

  internal string RequestTypeID => RequestType.ID;

  internal string ResponseTypeID => ResponseType.ID;

  /// <summary>
  /// A service belongs to the assembly of its request type.
  /// </summary>
  internal string Assembly => RequestType.Assembly;

  internal EvaTiming? Deprecated => EvaTiming.From(Extension(ExtensionNames.Deprecated));

  /// <summary>
  /// start for the operation that starts an asynchronous call, result for the one that returns its result.
  /// </summary>
  internal string? Async => Extension(ExtensionNames.Async)?.GetValue<string>();

  internal EvaAuth Auth
  {
    get
    {
      var auth = Extension(ExtensionNames.Auth)!;
      return new EvaAuth(
        auth["userTypes"]?.AsArray().Select(u => Enum.Parse<EvaUserTypes>(u!.GetValue<string>())).Aggregate((a, b) => a | b),
        auth["functionality"]?.GetValue<string>(),
        auth["scope"] is { } scope ? Enum.Parse<EvaFunctionalityScope>(scope.GetValue<string>()) : null,
        auth["allowPublic"]?.GetValue<bool>() ?? false);
    }
  }

  private System.Text.Json.Nodes.JsonNode? Extension(string name) => (Operation.Extensions.Find(name) as JsonNodeExtension)?.Node;

  private static OpenApiSchemaReference SchemaOf(IDictionary<string, IOpenApiMediaType> content) => (OpenApiSchemaReference)content["application/json"].Schema!;

  public override string ToString() => Name;
}
