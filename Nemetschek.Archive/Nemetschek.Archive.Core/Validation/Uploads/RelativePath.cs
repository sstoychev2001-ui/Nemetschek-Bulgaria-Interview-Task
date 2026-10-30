namespace Nemetschek.Archive.Core.Validation.Uploads;

/// <summary>
/// Reads the path an uploaded entry had inside the uploaded folder.
/// </summary>
internal static class RelativePath
{
    /// <summary>
    /// Splits a path into its name segments. Returns <c>null</c> if it is not a path inside the
    /// uploaded folder.
    /// </summary>
    public static IReadOnlyList<string>? TrySplit(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRooted(path))
        {
            return null;
        }

        var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        return segments.Length > 0 && Array.TrueForAll(segments, IsUsableName) ? segments : null;
    }

    /// <summary>Whether a string is usable as a single entry name rather than a path.</summary>
    public static bool IsName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        !name.Contains('/') && !name.Contains('\\') &&
        IsUsableName(name);

    private static bool IsRooted(string path) =>
        path[0] is '/' or '\\' || (path.Length > 1 && path[1] == ':');

    private static bool IsUsableName(string segment) =>
        segment is not ("." or "..") && !segment.Any(char.IsControl);
}
