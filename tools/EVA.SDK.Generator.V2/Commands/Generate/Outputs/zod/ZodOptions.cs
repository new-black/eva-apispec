using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.zod;

public class ZodOptions
{

}

public class ZodOptionsBinder : BinderBase<ZodOptions>, IOptionProvider
{
  protected override ZodOptions GetBoundValue(BindingContext ctx)
  {
    return new ZodOptions
    {

    };
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield break;
  }
}