using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;

public class DotNetOptions : GenerateOptions
{
  public bool UseNativeDayOfWeek { get; set; }
  public string JsonSerializer { get; set; }
  public bool EnableCustomIdMode { get; set; }
}

public class DotNetOptionsBinder : BaseGenerateOptionsBinder<DotNetOptions>
{
  private Option<bool> UseNativeDayOfWeek = new Option<bool>(
    name: "--opt-native-dow",
    description: "Use the native DayOfWeek enum instead of the custom one"
  ).WithDefault(false);

  private Option<string> JsonSerializer = new Option<string>(
    name: "--opt-json-serializer",
    description: "What serializer to use for JSON serialization"
  ).FromAmong("newtonsoft").WithDefault("newtonsoft");

  private Option<bool> EnableCustomIdMode = new Option<bool>(
    name: "--opt-custom-ids",
    description: "Enable support for custom IDs through LongOrString"
  ).WithDefault(false);

  protected override IEnumerable<Option> GetOptions()
  {
    yield return UseNativeDayOfWeek;
    yield return JsonSerializer;
    yield return EnableCustomIdMode;
  }

  protected override void BuildOptions(DotNetOptions options, BindingContext bindingContext)
  {
    options.UseNativeDayOfWeek = UseNativeDayOfWeek.Value(bindingContext);
    options.JsonSerializer = JsonSerializer.Value(bindingContext)!;
    options.EnableCustomIdMode = EnableCustomIdMode.Value(bindingContext);
  }
}