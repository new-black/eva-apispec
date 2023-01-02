using System.CommandLine;
using System.Text.Json.Serialization;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.ListVersions;

public static class ListVersionsCommand
{
  public static void Register(Command command)
  {
    var listVersionsCommand = new Command("list-versions");
    command.AddCommand(listVersionsCommand);

    listVersionsCommand.SetHandler(async logger =>
    {
      var result = await HttpHelpers.GetJson(HttpConstants.RefsUrl, JsonContext.Default.RefsOutputArray, logger);
      foreach (var reference in result.Select(x => x.Ref).FilterNotNull().Where(x => x.StartsWith("refs/tags/spec/")))
      {
        Console.WriteLine($"- {reference[15..]}");
      }
    }, LogBinder.Instance);
  }
}

internal class RefsOutput
{
  [JsonPropertyName("ref")] public string? Ref { get; set; }
}