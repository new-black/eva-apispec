namespace EVA.SDK.Generator.V2.Helpers;

public static class EnumerableExtensions
{
  public static IEnumerable<T> FilterNotNull<T>(this IEnumerable<T?> source)
  {
    return source.Where(x => x != null)!;
  }
}