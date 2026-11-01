using System.Globalization;

namespace Nemetschek.Archive.Core.Utils.Documents;

/// <summary>
/// The names and formats the Archive's storage layout is built from, and the two parsers that read
/// them back.
/// </summary>
public static class DocumentStorageUtils
{
    /// <summary>The directory every cabinet's documents sit under.</summary>
    public const string CabinetsFolder = "cabinets";

    /// <summary>The directory one document's part content sits in.</summary>
    public const string PartsFolder = "parts";

    /// <summary>The file one document's index values and part list sit in.</summary>
    public const string MetadataFileName = "document.json";

    /// <summary>How a day directory's name is written and read.</summary>
    public const string DayFormat = "yyyy-MM-dd";

    /// <summary>How the instant in a document directory's name is written and read.</summary>
    public const string InstantFormat = "yyyyMMddTHHmmssfffZ";

    private const string TempFolder = "tmp";

    /// <summary>The directory anything in flight is written into before it is committed.</summary>
    public static string Staging(string root) => Path.Combine(root, TempFolder);

    /// <summary>A staging path nothing else is using, for a directory or for a file.</summary>
    public static string StagingPath(string root, string extension = "") =>
        Path.Combine(Staging(root), $"{Guid.NewGuid():N}{extension}");

    /// <summary>The directory one cabinet's documents sit under.</summary>
    /// <exception cref="ArgumentException">
    /// The name cannot be a single directory name. Defence in depth: the catalog has already proved
    /// the cabinet exists by the time the store is called, so this can only fire if something
    /// upstream stops checking.
    /// </exception>
    public static string CabinetDirectory(string root, string cabinetName) =>
        cabinetName != Path.GetFileName(cabinetName) ||
        cabinetName is "" or "." or ".." ||
        cabinetName.Any(Path.GetInvalidFileNameChars().Contains)
            ? throw new ArgumentException($"File cabinet name '{cabinetName}' cannot be used as a directory name.")
            : Path.Combine(root, CabinetsFolder, cabinetName);

    /// <summary>The file one document's metadata sits in.</summary>
    public static string MetadataFile(string documentDirectory) =>
        Path.Combine(documentDirectory, MetadataFileName);

    /// <summary>The directory one document's part content sits in.</summary>
    public static string PartsDirectory(string documentDirectory) =>
        Path.Combine(documentDirectory, PartsFolder);

    /// <summary>The UTC day a day directory holds, or <c>null</c> if its name is not one.</summary>
    public static DateOnly? DayOf(string dayDirectory) =>
        DateOnly.TryParseExact(
            Path.GetFileName(dayDirectory),
            DayFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var day)
            ? day
            : null;

    /// <summary>The instant a document directory's name records, or <c>null</c>.</summary>
    public static DateTimeOffset? StoredAtOf(string documentDirectory)
    {
        var name = Path.GetFileName(documentDirectory);
        var separator = name.IndexOf('_', StringComparison.Ordinal);

        return separator > 0 && DateTimeOffset.TryParseExact(
            name[..separator],
            InstantFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var storedAt)
            ? storedAt
            : null;
    }
}
