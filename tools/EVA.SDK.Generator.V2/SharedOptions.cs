using System.CommandLine;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2;

internal static class SharedOptions
{
  internal static readonly Option<string> Input = new Option<string>(
    name: "--in",
    description: "The source of the input file. If not specified, will download the latest available."
  ).WithAlias("-i");

  internal static readonly OptionWithDefault<string> LogLevel = new Option<string>(
    name: "--log-level",
    description: "The log level to use."
  ).WithAlias("-l").FromAmong("trace", "debug", "info", "warning", "error", "none").WithDefault("info");
}