using System.CommandLine.Binding;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2;

public class LogBinder : BinderBase<ILogger>
{
  internal static readonly LogBinder Instance = new();

  protected override ILogger GetBoundValue(BindingContext bindingContext)
  {
    var loglevel = SharedOptions.LogLevel.Value(bindingContext);

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

    var name = bindingContext.ParseResult.CommandResult.Command.Name;
    return logger.CreateLogger(name);
  }
}