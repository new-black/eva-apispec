using System.CommandLine;
using System.CommandLine.Binding;

namespace EVA.SDK.Generator.V2.Commands.Generate.Helpers;

public static class OptionExtensions
{
  public static T? Value<T>(this Option<T> option, BindingContext ctx)
  {
    return ctx.ParseResult.GetValueForOption(option);
  }

  public static Option<T> WithDefault<T>(this Option<T> option, T defaultValue)
  {
    option.SetDefaultValue(defaultValue);
    return option;
  }
}