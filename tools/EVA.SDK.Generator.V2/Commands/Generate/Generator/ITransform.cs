using EVA.API.Spec;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator;

public interface ITransform
{
  [Flags]
  public enum TransformResult
  {
    Changes = 1,
    NoChanges = 0
  }

  TransformResult Transform(ApiDefinitionModel input, GenerateOptions options);
}