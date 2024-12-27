using System.CommandLine;
using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs.dotnet;

internal class DotNetOptions : GenerateOptions
{
  internal bool UseNativeDayOfWeek { get; set; } = DotNetOptionsBinder.UseNativeDayOfWeek.Default;
  public bool GenerateExtensions { get; set; } = DotNetOptionsBinder.GenerateExtensions.Default;
  public bool GenerateDynamicData { get; set; } = DotNetOptionsBinder.GenerateDynamicData.Default;
  public bool AddEvaClient { get; set; } = DotNetOptionsBinder.AddEvaClient.Default;
}

internal class DotNetOptionsBinder : BaseGenerateOptionsBinder<DotNetOptions>
{
  internal static readonly OptionWithDefault<bool> UseNativeDayOfWeek = new Option<bool>(
    name: "--opt-native-dow",
    description: "Use the native DayOfWeek enum instead of the custom one"
  ).WithDefault(false);

  internal static readonly OptionWithDefault<bool> GenerateExtensions = new Option<bool>(
    name: "--opt-extensions",
    description: "Generate extension methods"
  ).WithDefault(false);

  internal static readonly OptionWithDefault<bool> GenerateDynamicData = new Option<bool>(
    name: "--opt-dynamic-data",
    description: "Generate dynamic data support"
  ).WithDefault(true);

  internal static readonly OptionWithDefault<bool> AddEvaClient = new Option<bool>(
    name: "--opt-eva-client",
    description: "Add EVA client"
  ).WithDefault(false);

  protected override IEnumerable<Option> GetOptions()
  {
    yield return UseNativeDayOfWeek.Option;
    yield return GenerateExtensions.Option;
    yield return GenerateDynamicData.Option;
    yield return AddEvaClient.Option;
  }

  protected override void BuildOptions(DotNetOptions options, BindingContext ctx)
  {
    options.UseNativeDayOfWeek = UseNativeDayOfWeek.Value(ctx);
    options.GenerateExtensions = GenerateExtensions.Value(ctx);
    options.GenerateDynamicData = GenerateDynamicData.Value(ctx);
    options.AddEvaClient = AddEvaClient.Value(ctx);
  }
}