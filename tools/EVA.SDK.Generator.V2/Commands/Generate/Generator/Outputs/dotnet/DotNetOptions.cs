using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.dotnet;

public class DotNetOptions
{

}

public class DotNetOptionsBinder : BinderBase<DotNetOptions>, IOptionProvider
{
  protected override DotNetOptions GetBoundValue(BindingContext bindingContext)
  {
    return new DotNetOptions();
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield break;
  }
}