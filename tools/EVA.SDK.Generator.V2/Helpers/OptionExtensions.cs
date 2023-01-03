using System.CommandLine;
using System.CommandLine.Binding;

namespace EVA.SDK.Generator.V2.Helpers;

internal static class OptionExtensions
{
  internal static T? Value<T>(this Option<T> option, BindingContext ctx)
  {
    return ctx.ParseResult.GetValueForOption(option);
  }

  internal static T Value<T>(this OptionWithDefault<T> o, BindingContext ctx)
  {
    return ctx.ParseResult.GetValueForOption(o.Option)!;
  }

  internal static OptionWithDefault<T> WithDefault<T>(this Option<T> option, T defaultValue)
  {
    option.SetDefaultValue(defaultValue);
    return new OptionWithDefault<T>(option, defaultValue);
  }

  internal static Option<T> WithAlias<T>(this Option<T> option, string alias)
  {
    option.AddAlias(alias);
    return option;
  }
}

internal record OptionWithDefault<T>(Option<T> Option, T Default);