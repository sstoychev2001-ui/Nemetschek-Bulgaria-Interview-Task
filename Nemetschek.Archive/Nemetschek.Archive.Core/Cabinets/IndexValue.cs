namespace Nemetschek.Archive.Core.Cabinets;

/// <summary>
/// A value held in an index field. One case per <see cref="FieldType"/>, so that a value carries
/// its own type and the rules never have to re-parse a string.
/// </summary>
public abstract record IndexValue
{
    private IndexValue()
    {
    }

    /// <summary>The type this value can be stored in.</summary>
    public abstract FieldType Type { get; }

    /// <summary>A text value. Length is checked against the field's MaxLength.</summary>
    public sealed record Text(string Value) : IndexValue
    {
        public override FieldType Type => FieldType.Text;
    }

    /// <summary>A numeric value. <see cref="decimal"/> because the fixtures' numbers are amounts.</summary>
    public sealed record Number(decimal Value) : IndexValue
    {
        public override FieldType Type => FieldType.Number;
    }

    /// <summary>
    /// A calendar date. <see cref="DateOnly"/> on purpose: an index date has no time and no offset,
    /// so it cannot drift when it crosses a time zone.
    /// </summary>
    public sealed record Date(DateOnly Value) : IndexValue
    {
        public override FieldType Type => FieldType.Date;
    }
}
