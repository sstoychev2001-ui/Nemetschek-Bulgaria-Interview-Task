using System.Text;
using Nemetschek.Export.Core.Domain.Exports;
using Nemetschek.Export.Core.Clients.Interfaces;

namespace Nemetschek.Export.Core.Tests.Exports;

/// <summary>
/// An <see cref="IArchiveClient"/> that answers from memory and records what it was asked.
/// </summary>
internal sealed class FakeArchive : IArchiveClient
{
    private readonly Dictionary<Guid, string> content = new();

    public List<ArchiveDocument> Documents { get; } = [];

    public List<(string Cabinet, Guid DocumentId, string Field, string Value)> Marked { get; } = [];

    /// <summary>How many requests were made to write the marker.</summary>
    public int MarkRequests { get; private set; }

    public (string Cabinet, DateOnly From, DateOnly To)? AskedFor { get; private set; }

    public Exception? FailOpeningPart { get; init; }

    /// <summary>Adds a document whose parts hold the given text, one entry per part name.</summary>
    public ArchiveDocument Add(string title, params string[] partNames)
    {
        var parts = partNames
            .Select(name =>
            {
                var part = new ArchivePart(Guid.CreateVersion7(), name);
                content[part.Id] = $"content of {title}/{name}";

                return part;
            })
            .ToList();

        var document = new ArchiveDocument(Guid.CreateVersion7(), title, parts);
        Documents.Add(document);

        return document;
    }

    public async IAsyncEnumerable<ArchiveDocument> ReadDocumentsAsync(
        string cabinetName,
        DateOnly from,
        DateOnly to,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        AskedFor = (cabinetName, from, to);

        foreach (var document in Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return document;

            await Task.Yield();
        }
    }

    public Task<Stream> OpenPartAsync(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken) =>
        FailOpeningPart is { } failure
            ? Task.FromException<Stream>(failure)
            : Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content[partId])));

    public Task SetTextIndexValueAsync(
        string cabinetName,
        IReadOnlyCollection<Guid> documentIds,
        string fieldName,
        string value,
        CancellationToken cancellationToken)
    {
        MarkRequests++;

        foreach (var documentId in documentIds)
        {
            Marked.Add((cabinetName, documentId, fieldName, value));
        }

        return Task.CompletedTask;
    }
}
