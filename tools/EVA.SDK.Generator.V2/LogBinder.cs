using System.CommandLine;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2;

public static class LogBinder
{
  internal static ILogger Create(ParseResult parseResult)
  {
    var loglevel = SharedOptions.LogLevel.Value(parseResult);

    var logger = LoggerFactory.Create(builder =>
    {
      builder.AddSimpleConsole(o =>
      {
        o.IncludeScopes = true;
        o.SingleLine = true;
        o.TimestampFormat = "hh:mm:ss ";
      });
      builder.SetMinimumLevel(loglevel switch
      {
        "error" => LogLevel.Error,
        "warning" => LogLevel.Warning,
        "info" => LogLevel.Information,
        "debug" => LogLevel.Debug,
        "trace" => LogLevel.Trace,
        "none" => LogLevel.None,
        _ => LogLevel.None
      });
    });

    var name = parseResult.CommandResult.Command.Name;
    return logger.CreateLogger(name);
  }
}
