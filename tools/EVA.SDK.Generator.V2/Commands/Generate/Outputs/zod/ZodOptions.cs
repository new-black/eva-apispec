using System.CommandLine;
using System.CommandLine.Binding;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;

internal class ZodOptions : GenerateOptions
{
}

internal class ZodOptionsBinder : BaseGenerateOptionsBinder<ZodOptions>
{

  protected override IEnumerable<Option> GetOptions()
  {
    yield break;
  }

  protected override void BuildOptions(ZodOptions options, BindingContext ctx)
  {
  }
}