using System.CommandLine;
using System.Text.Json.Serialization;
using EVA.SDK.Generator.V2.Helpers;

namespace EVA.SDK.Generator.V2.Commands.ListVersions;

internal static class ListVersionsCommand
{
  internal static void Register(Command command)
  {
    var listVersionsCommand = new Command("list-versions");
    command.Add(listVersionsCommand);

    listVersionsCommand.SetAction(async (parseResult, _) =>
    {
      var logger = LogBinder.Create(parseResult);
      var result = await HttpHelpers.GetJson(HttpConstants.RefsUrl, JsonContext.Default.RefsOutputArray, logger);
      foreach (var reference in result.Select(x => x.Ref).FilterNotNull().Where(x => x.StartsWith("refs/tags/spec/")))
      {
        Console.WriteLine($"- {reference[15..]}");
      }
    });
  }
}

internal class RefsOutput
{
  [JsonPropertyName("ref")] public string? Ref { get; set; }
}