using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Utils.Documents;

namespace Nemetschek.Archive.Core.Services.Documents;

/// <summary>
/// Reading and writing one document's <c>document.json</c>.
/// </summary>
public static class DocumentMetadataFile
{
    private static readonly JsonSerializerOptions StoredJson =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Reads the document a directory holds.</summary>
    /// <exception cref="JsonException">The metadata is not valid JSON.</exception>
    /// <exception cref="ValidationException">The metadata does not describe a usable document.</exception>
    public static async Task<Document> ReadAsync(
        string documentDirectory,
        CancellationToken cancellationToken)
    {
        await using var metadata = File.OpenRead(DocumentStorageUtils.MetadataFile(documentDirectory));

        return (await JsonSerializer.DeserializeAsync<Document>(metadata, cancellationToken: cancellationToken))!;
    }

    /// <summary>Writes a document to a path of the caller's choosing.</summary>
    public static async Task WriteAsync(
        string path,
        Document document,
        CancellationToken cancellationToken)
    {
        await using var metadata = File.Create(path);

        await JsonSerializer.SerializeAsync(metadata, document, StoredJson, cancellationToken);
    }
}
