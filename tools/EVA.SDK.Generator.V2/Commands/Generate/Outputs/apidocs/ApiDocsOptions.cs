using System.CommandLine;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.apidocs;

internal class ApiDocsOptions : GenerateOptions
{
}

internal class ApiDocsOptionsBinder : BaseGenerateOptionsBinder<ApiDocsOptions>
{
  protected override IEnumerable<Option> GetOptions()
  {
    yield break;
  }

  protected override void BuildOptions(ApiDocsOptions options, ParseResult ctx)
  {
  }
}
