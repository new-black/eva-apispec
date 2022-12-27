using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.evaspec;

public class EvaSpecOptions : GenerateOptions
{
  public bool IncludeSrcFiles { get; set; }
}

public class EvaSpecOptionsBinder : BaseGenerateOptionsBinder<EvaSpecOptions>
{
  private Option<bool> IncludeSrcFiles = new(
    name: "--opt-include-src",
    description: "Includes formatted source files in output intended for human readability"
  );

  protected override IEnumerable<Option> GetOptions()
  {
    yield return IncludeSrcFiles;
  }

  protected override void BuildOptions(EvaSpecOptions options, BindingContext ctx)
  {
    options.IncludeSrcFiles = IncludeSrcFiles.Value(ctx);
  }
}