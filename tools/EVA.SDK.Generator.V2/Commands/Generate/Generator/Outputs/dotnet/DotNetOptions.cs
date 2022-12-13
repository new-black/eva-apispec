using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Commands.Generate.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Generator.Outputs.dotnet;

public class DotNetOptions
{
  public bool UseNativeDayOfWeek { get; set; }
  public string JsonSerializer { get; set; }
}

public class DotNetOptionsBinder : BinderBase<DotNetOptions>, IOptionProvider
{
  private Option<bool> UseNativeDayOfWeek = new Option<bool>(
    name: "--opt-native-dow",
    description: "Use the native DayOfWeek enum instead of the custom one"
  ).WithDefault(false);

  private Option<string> JsonSerializer = new Option<string>(
    name: "--opt-json-serializer",
    description: "What serializer to use for JSON serialization"
  ).FromAmong("newtonsoft").WithDefault("newtonsoft");

  protected override DotNetOptions GetBoundValue(BindingContext bindingContext)
  {
    return new DotNetOptions
    {
      UseNativeDayOfWeek = UseNativeDayOfWeek.Value(bindingContext),
      JsonSerializer = JsonSerializer.Value(bindingContext)!
    };
  }

  public IEnumerable<Option> GetAllOptions()
  {
    yield return UseNativeDayOfWeek;
    yield return JsonSerializer;
  }
}