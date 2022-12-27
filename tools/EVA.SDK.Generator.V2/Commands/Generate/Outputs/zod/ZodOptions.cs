using System.CommandLine;
using System.CommandLine.Binding;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;

public class ZodOptions : GenerateOptions
{

}

public class ZodOptionsBinder : BaseGenerateOptionsBinder<ZodOptions>
{
  protected override IEnumerable<Option> GetOptions()
  {
    yield break;
  }

  protected override void BuildOptions(ZodOptions options, BindingContext bindingContext)
  {

  }
}