namespace EVA.API.Spec;

public static class ApiSpecConsts
{
  public const string String = "string";
  public const string Int16 = "int16";
  public const string Int32 = "int32";
  public const string Int64 = "int64";
  public const string Float32 = "float32";
  public const string Float64 = "float64";
  public const string Float128 = "float128";
  public const string Date = "date";
  public const string Guid = "guid";
  public const string Bool = "bool";
  public const string Duration = "duration";
  public const string Object = "object";
  public const string Any = "any";
  public const string Binary = "binary";

  public static readonly HashSet<string> AllPrimitives = new()
  {
    String,
    Int16,
    Int32,
    Int64,
    Float32,
    Float64,
    Float128,
    Date,
    Guid,
    Bool,
    Duration,
    Object,
    Any,
    Binary
  };

  public static class Specials
  {
    public const string Array = "array";
    public const string Map = "map";
    public const string Option = "option";
  }

  public static class WellKnown
  {
    public const string IProductSearchItem = "EVA.Core.Search.IProductSearchItem";
    public const string DayOfWeek = "EVA.Core.DayOfWeek";
    public const string CoreAssembly = "EVA.Core";
  }
}