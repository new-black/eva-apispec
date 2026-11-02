using EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Transforms;
using EVA.SDK.Generator.V2.Commands.Generate.Outputs.swift;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Outputs.swift;

internal class SwiftOutput : IApiOutput<SwiftOptions>
{
  public string? OutputPattern => null;

  public bool GetForcedTransformations(SwiftOptions options, IApiTransform transform) => false;

  public Task Write(ApiOutputContext<SwiftOptions> ctx) => throw new NotImplementedException();
}
