using System.Text;
using EVA.SDK.Generator.V2.Helpers;
using Microsoft.Extensions.Logging;

namespace EVA.SDK.Generator.V2.Commands.Generate.Outputs;

internal class OutputWriter
{
  private readonly string _directory;
  private readonly ILogger _logger;

  private readonly Dictionary<string, (Result result, long size)> _writtenFiles = new();

  internal enum Result
  {
    Overwritten,
    Kept
  }

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

    // Only overwrite if file doesn't exist or content is different
    if (!File.Exists(path) || await File.ReadAllTextAsync(path) != content)
    {
      await File.WriteAllTextAsync(path, content);
      _writtenFiles.Add(file, (Result.Overwritten, new FileInfo(path).Length));
    }
    else
    {
      _writtenFiles.Add(file, (Result.Kept, new FileInfo(path).Length));
    }
  }

  internal DisposableCallback<Stream> WriteStreamAsync(string file)
  {
    file = FixFileName(file);

    var path = Path.Combine(_directory, file);
    EnsureDirectoryExists(path);
    return new DisposableCallback<Stream>(File.OpenWrite(path), () =>
    {
      _writtenFiles.Add(file, (Result.Overwritten, new FileInfo(path).Length));
    });
  }

  internal void DeleteRemaining()
  {
    var allWritten = _writtenFiles.Keys.Select(x => Path.GetFullPath(x, _directory)).ToHashSet();

    foreach(var x in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories))
    {
      if (!allWritten.Contains(x))
      {
        File.Delete(x);
      }
    }
  }

  internal void WriteReport(ILogger logger)
  {
    var sb = new StringBuilder();

    var keptFiles = _writtenFiles.Values.Count(x => x.result == Result.Kept);
    logger.LogInformation($"No change to {keptFiles} files");

    var changedFiles = _writtenFiles
      .Where(x => x.Value.result == Result.Overwritten)
      .Select(x => (name: x.Key, x.Value.result, x.Value.size))
      .ToList();

    var totalSize = changedFiles.Sum(x => x.size);
    logger.LogInformation($"Wrote {changedFiles.Count} files with total size of {StringHelpers.FormatSize(totalSize)}");
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