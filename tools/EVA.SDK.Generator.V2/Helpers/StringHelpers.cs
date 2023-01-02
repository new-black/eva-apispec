namespace EVA.SDK.Generator.V2.Helpers;

public class StringHelpers
{
  public static string FormatSize(long size)
  {
    var suffixes = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
    var suffixIndex = 0;
    while (size >= 1024 && suffixIndex < suffixes.Length - 1)
    {
      suffixIndex++;
      size /= 1024;
    }

    return $"{size:0.##} {suffixes[suffixIndex]}";
  }
}