namespace Nemetschek.Archive.Core.Domain.IndexFields;

/// <summary>
/// A value held in an index field. One case per <see cref="FieldType"/>, so that a value carries
/// its own type and the rules never have to re-parse a string.
/// </summary>
public abstract record IndexValue
{
    /// <summary>The type this value can be stored in.</summary>
    public abstract FieldType Type { get; }

    /// <summary>A text value.</summary>
    public sealed record Text(string Value) : IndexValue
    {
        public override FieldType Type => FieldType.Text;
    }

    /// <summary>A numeric value.</summary>
    public sealed record Number(decimal Value) : IndexValue
    {
        public override FieldType Type => FieldType.Number;
    }

    /// <summary>A calendar date.</summary>.
    public sealed record Date(DateOnly Value) : IndexValue
    {
        public override FieldType Type => FieldType.Date;
    }
}
