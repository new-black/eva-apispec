using System.Collections.Immutable;

namespace EVA.SDK.Generator.V2.Helpers;

internal static class CollectionExtensions
{
  internal static ImmutableArray<T> WithoutIndex<T>(this ImmutableArray<T> list, int idx)
  {
    var result = list.Where((_, i) => i != idx).ToList();
    return ImmutableArray.CreateRange(result);
  }

  internal static Dictionary<TKey, List<TSource>> ToLookupDictionary<TSource, TKey>(this IEnumerable<TSource> list, Func<TSource, TKey> keySelector)
    where TKey : notnull
  {
    return list.GroupBy(keySelector).ToDictionary(x => x.Key, x => x.ToList());
  }
}