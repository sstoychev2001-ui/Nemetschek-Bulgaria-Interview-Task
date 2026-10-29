using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Validation;

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

    // Both separators are checked whatever the server's platform: the path comes from the client, so
    // a Windows client talking to a Linux container must not produce one unsplit segment.
    private static bool IsRooted(string path) =>
        path[0] is '/' or '\\' || (path.Length > 1 && path[1] == ':');

    // "." and ".." are refused even though a segment is never used as a path, so that a traversal
    // attempt is reported to the user rather than quietly stored as a document title. Control
    // characters are refused because no real name contains one and they corrupt every consumer of
    // the report. Every other Unicode character is kept exactly as the user had it.
    private static bool IsUsableName(string segment) =>
        segment is not ("." or "..") && !segment.Any(char.IsControl);
}
