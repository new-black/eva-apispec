using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EVA.SDK.Generator.V2.Commands.ListVersions;

public class ListVersionsCommand
{
  public static void Register(Command command)
  {
    var listVersionsCommand = new Command("list-versions");
    command.AddCommand(listVersionsCommand);

    listVersionsCommand.SetHandler((Func<Task>)(async () =>
    {
      using var http = new HttpClient
      {
        DefaultRequestHeaders =
        {
          {"User-Agent", HttpConstants.UserAgent}
        }
      };
      using var response = await http.GetAsync(HttpConstants.RefsUrl);
      var result = await JsonSerializer.DeserializeAsync<RefsOutput[]>(await response.Content.ReadAsStreamAsync(), RefsOutputContext.Default.RefsOutputArray);

      foreach (var r in result.Where(x => x.Ref.StartsWith("refs/tags/spec/")))
      {
        Console.WriteLine($"- {r.Ref[15..]}");
      }
    }));
  }
}

internal class RefsOutput
{
  [JsonPropertyName("ref")]
  public string Ref { get; set; }
}

[JsonSerializable(typeof(RefsOutput[]))]
internal partial class RefsOutputContext : JsonSerializerContext { }