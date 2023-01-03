using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;

internal class DotNetOptions : GenerateOptions
{
  internal bool UseNativeDayOfWeek { get; set; } = DotNetOptionsBinder.UseNativeDayOfWeek.Default;
  internal string JsonSerializer { get; set; } = DotNetOptionsBinder.JsonSerializer.Default;
  internal bool EnableCustomIdMode { get; set; } = DotNetOptionsBinder.EnableCustomIdMode.Default;
}

internal class DotNetOptionsBinder : BaseGenerateOptionsBinder<DotNetOptions>
{
  internal static readonly OptionWithDefault<bool> UseNativeDayOfWeek = new Option<bool>(
    name: "--opt-native-dow",
    description: "Use the native DayOfWeek enum instead of the custom one"
  ).WithDefault(false);

  internal static readonly OptionWithDefault<string> JsonSerializer = new Option<string>(
    name: "--opt-json-serializer",
    description: "What serializer to use for JSON serialization"
  ).FromAmong("newtonsoft").WithDefault("newtonsoft");

  internal static readonly OptionWithDefault<bool> EnableCustomIdMode = new Option<bool>(
    name: "--opt-custom-ids",
    description: "Enable support for custom IDs through LongOrString"
  ).WithDefault(false);

  protected override IEnumerable<Option> GetOptions()
  {
    yield return UseNativeDayOfWeek.Option;
    yield return JsonSerializer.Option;
    yield return EnableCustomIdMode.Option;
  }

  protected override void BuildOptions(DotNetOptions options, BindingContext ctx)
  {
    options.UseNativeDayOfWeek = UseNativeDayOfWeek.Value(ctx);
    options.JsonSerializer = JsonSerializer.Value(ctx);
    options.EnableCustomIdMode = EnableCustomIdMode.Value(ctx);
  }
}