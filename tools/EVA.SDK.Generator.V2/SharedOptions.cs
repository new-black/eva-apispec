using System.CommandLine;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2;

public static class SharedOptions
{
  public static Option<string> Input = new Option<string>(
    name: "--in",
    description: "The source of the input file. If not specified, will download the latest available."
  ).WithAlias("-i");
}