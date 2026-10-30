using System.Text;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Services.Documents;

namespace Nemetschek.Archive.Core.Tests.Documents;

/// <summary>
/// What the store writes down. Runs against a throwaway directory: no container, no network.
/// </summary>
// The failure test is the one that matters: UploadService catches a store exception and turns it
// into a Failed row, so if the LogError went missing, a disk that had stopped accepting writes
// would answer 200 and leave nothing in the container log. That is how the behaviour was lost once
// already — see the UploadFailureStatusMiddleware note in DECISIONS.md.
public sealed class DocumentsStoreServiceTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), $"archive-tests-{Guid.NewGuid():N}");

    private readonly RecordingLogger<DocumentsStoreService> logger = new();

    /// <summary>A store on a prepared, empty storage root.</summary>
    private DocumentsStoreService Store()
    {
        DocumentsStoreService.Prepare(root);

        return new DocumentsStoreService(root, TimeProvider.System, logger);
    }

    [Fact]
    public async Task A_stored_document_is_logged_with_its_id()
    {
        var stored = await Store().AddAsync(
            Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(stored.Id.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains("Invoices", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_store_failure_is_logged_with_its_exception_and_rethrown()
    {
        // A part on its way in must carry OpenContent; one without it is the store's own
        // InvalidOperationException, raised after the staging directory already exists.
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Store().AddAsync(Draft(new Part { Name = "page1.txt" }), CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
    }

    [Fact]
    public async Task A_cancelled_store_is_not_logged_as_a_failure()
    {
        // A caller going away is not a fault of the Archive's, and logging it at Error would make
        // an abandoned upload look like a broken volume.
        var draft = Draft(Part.ToStore("page1.txt", () => throw new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Store().AddAsync(draft, CancellationToken.None));

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task A_failed_store_leaves_nothing_staged()
    {
        var store = Store();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AddAsync(Draft(new Part { Name = "page1.txt" }), CancellationToken.None));

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    [Fact]
    public async Task Reading_a_page_logs_nothing()
    {
        // Reads are accounted for by the per-request HTTP log, so the store stays quiet.
        await Store().ListAsync("Invoices", skip: 0, take: 10, CancellationToken.None);

        Assert.Empty(logger.Entries);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Func<Stream> Content =>
        () => new MemoryStream(Encoding.UTF8.GetBytes("page one"));

    private static Document Draft(Part part) => Document.ToStore(
        "Invoices",
        "invoice-001.txt",
        new Dictionary<string, IndexValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Batch"] = new IndexValue.Text("sample-material"),
        },
        [part]);

    /// <summary>An <see cref="ILogger{T}"/> that keeps every entry written to it.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
