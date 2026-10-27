using System.CommandLine;

namespace EVA.SDK.Generator.V2.Helpers;

internal static class OptionExtensions
{
  internal static T? Value<T>(this Option<T> option, ParseResult ctx)
  {
    return ctx.GetValue(option);
  }

  internal static T Value<T>(this OptionWithDefault<T> o, ParseResult ctx)
  {
    return ctx.GetValue(o.Option)!;
  }

  internal static OptionWithDefault<T> WithDefault<T>(this Option<T> option, T defaultValue)
  {
    option.DefaultValueFactory = _ => defaultValue;
    return new OptionWithDefault<T>(option, defaultValue);
  }

  internal static Option<T> WithAlias<T>(this Option<T> option, string alias)
  {
    option.Aliases.Add(alias);
    return option;
  }
}

internal record OptionWithDefault<T>(Option<T> Option, T Default);
