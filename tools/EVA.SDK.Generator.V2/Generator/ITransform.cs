using EVA.Core.Typings.V2;

namespace EVA.SDK.Generator.V2.Generator;

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