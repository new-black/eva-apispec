using System.Text.Json;
using System.Text.Json.Serialization;
using EVA.API.Spec;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;
using EVA.SDK.Generator.V2.Commands.ListVersions;
using EVA.SDK.Generator.V2.Commands.Update;
using EVA.SDK.Generator.V2.Inputs;

namespace EVA.SDK.Generator.V2;

[JsonSerializable(typeof(RefsOutput[]))]
[JsonSerializable(typeof(ApiDefinitionModel))]
[JsonSerializable(typeof(ServiceModel))]
[JsonSerializable(typeof(TypeSpecification))]
[JsonSerializable(typeof(HttpInput.EvaResponse))]
[JsonSerializable(typeof(UpdateCommand.LatestResponse))]
[JsonSerializable(typeof(SidebarItem[]))]
[JsonSerializable(typeof(ServiceItem))]
internal partial class JsonContext : JsonSerializerContext
{
  internal static JsonContext Indented { get; } = new(new JsonSerializerOptions { WriteIndented = true });
}