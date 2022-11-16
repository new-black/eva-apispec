using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.typescript;

public class TypescriptOptions
{

}

public class TypescriptOptionsBinder : BinderBase<TypescriptOptions>, IOptionProvider
{
  protected override TypescriptOptions GetBoundValue(BindingContext bindingContext)
  {
    return new TypescriptOptions();
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield break;
  }
}