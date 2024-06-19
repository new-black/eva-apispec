using System.Text;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs;

internal class OutputWriter
{
  private readonly string _directory;
  private readonly ILogger _logger;

  private readonly Dictionary<string, long> _writtenFiles = new();

  internal OutputWriter(string directory, ILogger logger)
  {
    _directory = directory;
    _logger = logger;
  }

  private static void EnsureDirectoryExists(string file)
  {
    var directory = Path.GetDirectoryName(file);
    if (directory == null) return;
    if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
  }

  private string FixFileName(string file)
  {
    if (!_writtenFiles.ContainsKey(file))
    {
      return file;
    }

    // Find an alternative name
    var suffix = 1;
    while (true)
    {
      var newfile = Path.Combine(Path.GetDirectoryName(file) ?? throw new InvalidOperationException(), Path.GetFileNameWithoutExtension(file) + $".{suffix++}" + Path.GetExtension(file));
      if (!_writtenFiles.ContainsKey(newfile))
      {
        _logger.LogError("Duplicate file, renamed {File} -> {File2}", file, newfile);
        return newfile;
      }
    }
  }

  internal async Task WriteFileAsync(string file, string content)
  {
    file = FixFileName(file);

    var path = Path.Combine(_directory, file);
    EnsureDirectoryExists(path);
    await File.WriteAllTextAsync(path, content);
    _writtenFiles.Add(file, new FileInfo(path).Length);
  }

  internal DisposableCallback<Stream> WriteStreamAsync(string file)
  {
    file = FixFileName(file);

    var path = Path.Combine(_directory, file);
    EnsureDirectoryExists(path);
    return new DisposableCallback<Stream>(File.OpenWrite(path), () =>
    {
      _writtenFiles.Add(file, new FileInfo(path).Length);
    });
  }

  internal string ToReport()
  {
    var sb = new StringBuilder();

    var totalSize = _writtenFiles.Values.Sum();
    sb.Append("Wrote ").Append(_writtenFiles.Count).Append(" files with total size of ").Append(StringHelpers.FormatSize(totalSize));
    if (_writtenFiles.Count is <= 1 or > 40) return sb.ToString();

    var records = _writtenFiles
      .Select(f => (name: f.Key, sizeStr: StringHelpers.FormatSize(f.Value), size: f.Value))
      .OrderByDescending(x => x.size)
      .ToList();
    var sizeColumnWidth = records.Max(x => x.sizeStr.Length);

    sb.AppendLine(":");

    foreach (var f in records)
    {
      sb.Append("  ").Append(f.sizeStr.PadLeft(sizeColumnWidth)).Append(' ').Append(((int)(100.0f * f.size / totalSize)).ToString().PadLeft(3)).Append("% ").AppendLine(f.name);
    }

    return sb.ToString();
  }

  internal class DisposableCallback<T> : IAsyncDisposable where T : IAsyncDisposable
  {
    private readonly Action _callback;

    internal T Value { get; }

    internal DisposableCallback(T wrapped, Action callback)
    {
      Value = wrapped;
      _callback = callback;
    }

    public async ValueTask DisposeAsync()
    {
      await Value.DisposeAsync();
      _callback.Invoke();
    }
  }
}