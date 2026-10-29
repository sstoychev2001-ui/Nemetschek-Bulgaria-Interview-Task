using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Domain.Documents;

/// <summary>
/// A document as it is handed to the store: everything but the system fields, which the store sets.
/// </summary>
/// <param name="CabinetName">The technical name of the file cabinet to store it in.</param>
/// <param name="Title">The name of the uploaded file or folder it came from.</param>
/// <param name="IndexValues">The index values, keyed by the field's technical name.</param>
/// <param name="Parts">The parts, in the order they are to be stored.</param>
public sealed record DocumentDraft(
    string CabinetName,
    string Title,
    IReadOnlyDictionary<string, IndexValue> IndexValues,
    IReadOnlyList<PartDraft> Parts);
