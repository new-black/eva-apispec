using System.Collections.Immutable;

namespace EVA.SDK.Generator.V2.Commands.Generate.OpenApi.Model;

internal enum PrimitiveKind
{
  String,

  /// <summary>The ID of an EVA entity, always exposed as a string.</summary>
  ID,
  Int16,
  Int32,
  Int64,
  Float32,
  Float64,
  Decimal,
  Date,
  Guid,
  Bool,
  Duration,
  Binary,

  /// <summary>A JSON object with any properties.</summary>
  Object,

  /// <summary>Any JSON value.</summary>
  Any
}

/// <summary>
/// The type of a property, base type, generic argument or error argument, as described by its schema.
/// </summary>
internal abstract record TypeRef(bool Nullable)
{
  internal abstract TypeRef WithNullable(bool nullable);

  /// <summary>
  /// All references in this one, this one included.
  /// </summary>
  internal IEnumerable<TypeRef> Descendants()
  {
    yield return this;

    var children = this switch
    {
      ArrayRef a => [a.Items],
      MapRef m => [m.Key, m.Value],
      NamedRef n => n.Arguments,
      OptionRef o => [..o.Options, o.Shared],
      _ => Enumerable.Empty<TypeRef>()
    };

    foreach (var x in children.SelectMany(c => c.Descendants())) yield return x;
  }
}

internal sealed record PrimitiveRef(PrimitiveKind Kind, bool Nullable) : TypeRef(Nullable)
{
  internal override TypeRef WithNullable(bool nullable) => this with { Nullable = nullable };
}

internal sealed record ArrayRef(TypeRef Items, bool Nullable) : TypeRef(Nullable)
{
  internal override TypeRef WithNullable(bool nullable) => this with { Nullable = nullable };
}

/// <summary>
/// A map from <see cref="Key"/> to <see cref="Value"/>. In JSON the keys are always strings.
/// </summary>
internal sealed record MapRef(TypeRef Key, TypeRef Value, bool Nullable) : TypeRef(Nullable)
{
  internal override TypeRef WithNullable(bool nullable) => this with { Nullable = nullable };
}

/// <summary>
/// A type of the API. For a constructed generic type, <see cref="TypeID"/> is the generic type and <see cref="Arguments"/> its arguments.
/// </summary>
internal sealed record NamedRef(string TypeID, ImmutableArray<TypeRef> Arguments, bool Nullable) : TypeRef(Nullable)
{
  internal override TypeRef WithNullable(bool nullable) => this with { Nullable = nullable };

  public bool Equals(NamedRef? other) => other != null && TypeID == other.TypeID && Nullable == other.Nullable && Arguments.SequenceEqual(other.Arguments);

  public override int GetHashCode() => HashCode.Combine(TypeID, Nullable, Arguments.Length);
}

/// <summary>
/// A type parameter of the generic type it is used in.
/// </summary>
internal sealed record TypeParameterRef(string Name, bool Nullable) : TypeRef(Nullable)
{
  internal override TypeRef WithNullable(bool nullable) => this with { Nullable = nullable };
}

/// <summary>
/// Any of the <see cref="Options"/>, which all are a <see cref="Shared"/>. Options of other assemblies may be left out, the shared type is
/// what remains.
/// </summary>
internal sealed record OptionRef(ImmutableArray<NamedRef> Options, TypeRef Shared, bool Nullable) : TypeRef(Nullable)
{
  internal override TypeRef WithNullable(bool nullable) => this with { Nullable = nullable };

  public bool Equals(OptionRef? other) => other != null && Shared == other.Shared && Nullable == other.Nullable && Options.SequenceEqual(other.Options);

  public override int GetHashCode() => HashCode.Combine(Shared, Nullable, Options.Length);
}
