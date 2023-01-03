namespace EVA.SDK.Generator.V2.Helpers;

internal static class EnumerableExtensions
{
  internal static IEnumerable<T> FilterNotNull<T>(this IEnumerable<T?> source)
  {
    return source.Where(x => x != null)!;
  }
}