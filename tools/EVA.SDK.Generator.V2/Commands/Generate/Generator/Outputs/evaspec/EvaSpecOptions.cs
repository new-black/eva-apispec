using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.evaspec;

public class EvaSpecOptions
{
  public bool IncludeSrcFiles { get; set; }
}

public class EvaSpecOptionsBinder : BinderBase<EvaSpecOptions>, IOptionProvider
{
  private Option<bool> IncludeSrcFiles = new(
    name: "--opt-include-src",
    description: "Includes formatted source files in output intended for human readability"
  );

  protected override EvaSpecOptions GetBoundValue(BindingContext ctx)
  {
    return new EvaSpecOptions
    {
      IncludeSrcFiles = IncludeSrcFiles.Value(ctx)
    };
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield return IncludeSrcFiles;
  }
}