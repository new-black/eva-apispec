using System.Collections.Immutable;

namespace EVA.SDK.Generator.V2.Commands.Generate.Helpers;

public static class CollectionExtensions
{
  public static int IndexOf<T>(this ImmutableArray<T> list, T value) where T : class
  {
    for (var i = 0; i < list.Length; i++)
    {
      if (list[i] == value) return i;
    }

    return -1;
  }

  public static ImmutableArray<T> WithoutIndex<T>(this ImmutableArray<T> list, int idx, bool nullIfEmpty = false)
  {
    var result = new List<T>();

    for (var i = 0; i < list.Length; i++)
    {
      if(i != idx) result.Add(list[i]);
    }

    return ImmutableArray.CreateRange(result);
  }
}