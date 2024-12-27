namespace EVA.SDK;

public partial class DynamicData<TShared> where TShared : class
{
  public static implicit operator TShared?(DynamicData<TShared> x)
  {
    return x.Shared;
  }
}