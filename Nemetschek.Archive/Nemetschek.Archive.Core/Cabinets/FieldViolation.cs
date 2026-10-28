namespace Nemetschek.Archive.Core.Cabinets;

/// <summary>
/// One reason a write to an index field was refused. Carries the field name so a caller can point
/// at the offending input, and a reason meant to be shown to a user unchanged — the client is not
/// allowed to restate server rules, so the server has to phrase them.
/// </summary>
/// <param name="FieldName">The technical name of the field that was refused.</param>
/// <param name="Reason">A complete, human-readable sentence naming the limit that was broken.</param>
public sealed record FieldViolation(string FieldName, string Reason);
